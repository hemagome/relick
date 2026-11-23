// Original Relick score: an orchestral synthesizer and the game's compositions.
// Build: g++ -O2 -std=c++17 -pthread tools/audio/score.cpp -o /tmp/relick-score
// Run: /tmp/relick-score /tmp/relick-audio-wav [--stems] [--only=yuno-oro|wd]
#include <algorithm>
#include <array>
#include <cmath>
#include <cstdint>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <memory>
#include <sstream>
#include <string>
#include <thread>
#include <vector>

constexpr int SR = 44100;
constexpr double PI = 3.14159265358979323846, TAU = 2 * PI;

struct Rng {
    uint32_t s;
    double uni() { s ^= s << 13; s ^= s >> 17; s ^= s << 5; return s * (1.0 / 4294967296.0); }
    double bi() { return uni() * 2 - 1; }
};
double hz(double midi) { return 440 * std::pow(2.0, (midi - 69) / 12.0); }
double smooth(double x) { x = std::clamp(x, 0.0, 1.0); return x * x * (3 - 2 * x); }

struct Bus {
    std::string name; double gain, send; std::vector<float> l, r;
    void add(long i, double v, double gl, double gr) {
        if (i < 0 || i >= long(l.size())) return;
        l[i] += float(v * gl); r[i] += float(v * gr);
    }
};
struct Pan { double l, r; Pan(double p) { double a = (std::clamp(p, -1.0, 1.0) + 1) * PI / 4; l = std::cos(a); r = std::sin(a); } };

// ---------------------------------------------------------------- instruments
// Bowed section: detuned band-limited saws, each player with its own vibrato.
void strings(Bus &b, Rng &g, double t0, double dur, double midi, double vel, double pan, bool spiccato, int players = 5, double bright = 1) {
    double f = hz(midi), attack = spiccato ? 0.008 : 0.06 + 0.06 * (1 - vel), release = spiccato ? 0.06 : 0.24;
    double hold = spiccato ? std::min(dur, 0.16) : dur, tau = 0.075 + 0.03 * vel;
    int n = int((hold + release) * SR); long i0 = std::lround(t0 * SR);
    double cutoff = std::min(15000.0, (spiccato ? 2300 : 1700) * bright * (0.55 + 0.9 * vel) + f * 1.6);
    double k = 1 - std::exp(-TAU * cutoff / SR), scale = vel * 1.5 / std::sqrt(double(players));
    for (int v = 0; v < players; v++) {
        double spread = players > 1 ? v / (players - 1.0) - 0.5 : 0;
        double detune = std::pow(2.0, (g.bi() * 7 + spread * 9) / 1200.0), phase = g.uni();
        double rate = 5.0 + g.uni() * 1.3, vibPhase = g.uni(), depth = spiccato ? 0 : 0.0032 * (0.5 + g.uni());
        Pan p(pan + spread * 0.45); double a = 0, c = 0;
        for (int i = 0; i < n; i++) {
            double t = double(i) / SR;
            double dt = f * detune * (1 + depth * smooth(t / 0.4) * std::sin(TAU * (rate * t + vibPhase))) / SR;
            phase += dt; if (phase >= 1) phase -= 1;
            double s = 2 * phase - 1;
            if (phase < dt) { double x = phase / dt; s -= x + x - x * x - 1; }
            else if (phase > 1 - dt) { double x = (phase - 1) / dt; s -= x * x + x + x + 1; }
            a += k * (s - a); c += k * (a - c);
            double e = spiccato ? (t < attack ? t / attack : std::exp(-(t - attack) / tau)) : smooth(t / attack);
            if (t > hold) e *= std::exp(-(t - hold) / (release * 0.3));
            e *= smooth((n - i) / 200.0);
            b.add(i0 + i, c * e * scale, p.l, p.r);
        }
    }
}

// Brass: one-to-one FM whose index opens with the attack, like a player's breath.
void brass(Bus &b, Rng &g, double t0, double dur, double midi, double vel, double pan, bool horn) {
    double f = hz(midi), attack = horn ? 0.065 : 0.03, release = 0.13, index = (horn ? 0.9 + 1.5 * vel : 1.5 + 3.0 * vel) * std::pow(2.0, -std::max(0.0, midi - 65) / 26);
    int n = int((dur + release) * SR), players = horn ? 3 : 2; long i0 = std::lround(t0 * SR);
    for (int v = 0; v < players; v++) {
        double detune = std::pow(2.0, (g.bi() * 6) / 1200.0), phase = g.uni(), vibPhase = g.uni();
        Pan p(pan + (v - (players - 1) / 2.0) * 0.2);
        for (int i = 0; i < n; i++) {
            double t = double(i) / SR;
            double vib = 1 + 0.003 * smooth((t - 0.25) / 0.35) * std::sin(TAU * (5.4 * t + vibPhase));
            phase += f * detune * (1 - 0.012 * std::exp(-t / 0.02)) * vib / SR;
            double open = t < attack ? t / attack : 0.72 + 0.28 * std::exp(-(t - attack) / 0.3);
            double s = std::sin(TAU * phase + index * open * std::sin(TAU * phase));
            double e = smooth(t / attack) * (t > dur ? std::exp(-(t - dur) / (release * 0.3)) : 1) * smooth((n - i) / 200.0);
            b.add(i0 + i, s * e * vel * 0.8 / std::sqrt(double(players)), p.l, p.r);
        }
    }
}

void flute(Bus &b, Rng &g, double t0, double dur, double midi, double vel, double pan) {
    double f = hz(midi), attack = 0.055, release = 0.16, phase = 0, vibPhase = g.uni(), lo = 0, hi = 0;
    int n = int((dur + release) * SR); long i0 = std::lround(t0 * SR); Pan p(pan);
    double kLo = 1 - std::exp(-TAU * f * 3 / SR), kHi = 1 - std::exp(-TAU * f / SR);
    for (int i = 0; i < n; i++) {
        double t = double(i) / SR, vib = smooth((t - 0.18) / 0.4) * std::sin(TAU * (5.1 * t + vibPhase));
        phase += f * (1 + 0.0045 * vib) / SR;
        double tone = std::sin(TAU * phase) + 0.32 * std::sin(TAU * 2 * phase) + 0.11 * std::sin(TAU * 3 * phase) + 0.04 * std::sin(TAU * 4 * phase);
        double white = g.bi(); lo += kLo * (white - lo); hi += kHi * (lo - hi);
        double breath = (lo - hi) * (0.10 + 0.5 * std::exp(-t / 0.03));
        double e = smooth(t / attack) * (1 + 0.08 * vib) * (t > dur ? std::exp(-(t - dur) / (release * 0.3)) : 1) * smooth((n - i) / 200.0);
        b.add(i0 + i, (tone * 0.62 + breath) * e * vel, p.l, p.r);
    }
}

// Plucked string (guitar, harp, pizzicato): a tuned Karplus-Strong loop.
void pluck(Bus &b, Rng &g, double t0, double midi, double vel, double pan, double t60, double dark, double dur = -1) {
    double f = hz(midi), period = SR / f, delay = period - 0.5; int size = int(delay); double frac = delay - size;
    if (frac < 0.1) { size--; frac += 1; }
    double coefficient = (1 - frac) / (1 + frac), rho = std::pow(10.0, -3 * period / (SR * t60));
    // The pick's brightness follows the fundamental; `dark` narrows it towards a soft fingertip.
    double k = 1 - std::exp(-TAU * std::min(9000.0, f * (2 + 14 * (1 - dark)) * (0.6 + 0.8 * vel)) / SR);
    std::vector<double> line(size); double lp = 0, lp2 = 0, peak = 1e-9, mean = 0;
    for (int pass = 0; pass < 2; pass++) for (auto &x : line) { lp += k * (g.bi() - lp); lp2 += k * (lp - lp2); x = lp2; }
    for (auto x : line) mean += x / size;
    for (auto &x : line) x -= mean;
    int pick = std::max(1, int(size * 0.23));
    std::vector<double> shaped(size);
    for (int i = 0; i < size; i++) { shaped[i] = line[i] - 0.7 * line[(i + size - pick) % size]; peak = std::max(peak, std::abs(shaped[i])); }
    for (int i = 0; i < size; i++) line[i] = shaped[i] / peak;
    int n = int(std::min(t60 * 1.1, dur > 0 ? dur + 0.25 : 1e9) * SR), index = 0; long i0 = std::lround(t0 * SR);
    double previous = 0, apIn = 0, apOut = 0; Pan p(pan);
    for (int i = 0; i < n; i++) {
        double x = line[index], average = 0.5 * (x + previous); previous = x;
        double out = coefficient * average + apIn - coefficient * apOut; apIn = average; apOut = out;
        line[index] = rho * out; index = (index + 1) % size;
        double t = double(i) / SR, e = smooth(i / 40.0) * smooth((n - i) / 2000.0);
        if (dur > 0 && t > dur) e *= std::exp(-(t - dur) / 0.05);
        b.add(i0 + i, x * e * vel * 0.9, p.l, p.r);
    }
}

// Piano: stretched partials on two slightly detuned strings, with a hammer thump.
void piano(Bus &b, Rng &g, double t0, double dur, double midi, double vel, double pan) {
    double f = hz(midi), base = 0.55 * std::pow(2.0, (midi - 60) / 16.0), inharmonic = 0.00035 * std::pow(2.0, (midi - 60) / 14.0);
    int partials = std::clamp(int(9500 / f), 1, 26), n = int((std::min(dur, 7.0) + 0.3) * SR); long i0 = std::lround(t0 * SR); Pan p(pan);
    std::vector<double> out(n, 0.0);
    for (int k = 1; k <= partials; k++) {
        double fk = f * k * std::sqrt(1 + inharmonic * k * k); if (fk > 16000) break;
        double amp = std::pow(k, -1.15) * std::exp(-k * (0.05 + 0.22 * (1 - vel))) * (1 - 0.6 * std::pow(std::cos(PI * k / 8.0), 8));
        double decay = base * (1 + 0.035 * k * k);
        for (int string = 0; string < 2; string++) {
            double w = TAU * fk * (1 + (string ? 0.0005 : -0.0005)) / SR, phase = g.uni() * TAU;
            double fast = std::exp(-decay * 2.6 / SR), slow = std::exp(-decay * 0.5 / SR), eFast = 0.65, eSlow = 0.35;
            for (int i = 0; i < n; i++) { out[i] += amp * 0.5 * (eFast + eSlow) * std::sin(phase + w * i); eFast *= fast; eSlow *= slow; }
        }
    }
    double lp = 0;
    for (int i = 0; i < n; i++) {
        double t = double(i) / SR; lp += 0.2 * (g.bi() - lp);
        double e = smooth(t / 0.003) * (t > dur ? std::exp(-(t - dur) / 0.07) : 1) * smooth((n - i) / 300.0);
        b.add(i0 + i, (out[i] + lp * 0.5 * std::exp(-t / 0.012)) * e * vel * 0.55, p.l, p.r);
    }
}

void bell(Bus &b, double t0, double midi, double vel, double pan) {
    double f = hz(midi); int n = int(1.6 * SR); long i0 = std::lround(t0 * SR); Pan p(pan);
    for (int i = 0; i < n; i++) {
        double t = double(i) / SR;
        double s = std::sin(TAU * f * t) * std::exp(-t * 3.2) + 0.32 * std::sin(TAU * f * 2.756 * t) * std::exp(-t * 6) + 0.12 * std::sin(TAU * f * 5.404 * t) * std::exp(-t * 11);
        b.add(i0 + i, s * smooth(i / 60.0) * smooth((n - i) / 300.0) * vel * 0.7, p.l, p.r);
    }
}

void sub(Bus &b, double t0, double dur, double midi, double vel) {
    double f = hz(midi); int n = int((dur + 0.08) * SR); long i0 = std::lround(t0 * SR);
    for (int i = 0; i < n; i++) {
        double t = double(i) / SR, e = smooth(t / 0.012) * (t > dur ? std::exp(-(t - dur) / 0.02) : 1) * smooth((n - i) / 200.0);
        b.add(i0 + i, (std::sin(TAU * f * t) + 0.25 * std::sin(TAU * 2 * f * t)) * e * vel * 0.7, 0.7071, 0.7071);
    }
}

void timpani(Bus &b, Rng &g, double t0, double midi, double vel, double pan = -0.15) {
    static const double ratio[6] = {1, 1.504, 1.742, 2.0, 2.245, 2.494}, level[6] = {1, 0.55, 0.22, 0.38, 0.14, 0.1}, rate[6] = {2.6, 4.2, 6, 5.5, 8, 9};
    double f = hz(midi), lp = 0, phases[6] = {0, 0, 0, 0, 0, 0}; int n = int(1.7 * SR); long i0 = std::lround(t0 * SR); Pan p(pan);
    for (int i = 0; i < n; i++) {
        double t = double(i) / SR, bend = 1 + 0.02 * vel * std::exp(-t / 0.05), s = 0;
        for (int m = 0; m < 6; m++) {
            phases[m] += f * ratio[m] * bend / SR;
            s += level[m] * (m ? 0.4 + 0.6 * vel : 1) * std::sin(TAU * phases[m]) * std::exp(-t * rate[m]);
        }
        lp += 0.06 * (g.bi() - lp);
        b.add(i0 + i, (s * 0.6 + lp * 2.2 * std::exp(-t / 0.018)) * smooth(i / 50.0) * smooth((n - i) / 400.0) * vel, p.l, p.r);
    }
}

void kick(Bus &b, Rng &g, double t0, double vel, bool concert) {
    double length = concert ? 1.0 : 0.3, low = concert ? 44 : 50, sweep = concert ? 40 : 120, fall = concert ? 0.05 : 0.028, decay = concert ? 5 : 15;
    int n = int(length * SR); long i0 = std::lround(t0 * SR); double phase = 0, lp = 0;
    for (int i = 0; i < n; i++) {
        double t = double(i) / SR; phase += (low + sweep * std::exp(-t / fall)) / SR; lp += 0.3 * (g.bi() - lp);
        double s = std::sin(TAU * phase) * std::exp(-t * decay) + lp * 0.35 * std::exp(-t * 180);
        b.add(i0 + i, s * smooth(i / 30.0) * smooth((n - i) / 300.0) * vel, 0.7071, 0.7071);
    }
}

void snare(Bus &b, Rng &g, double t0, double vel, double pan = 0.08) {
    int n = int(0.28 * SR); long i0 = std::lround(t0 * SR); double phase = 0, lp = 0, band = 0; Pan p(pan);
    for (int i = 0; i < n; i++) {
        double t = double(i) / SR; phase += (182 + 70 * std::exp(-t / 0.008)) / SR;
        double white = g.bi(); lp += 0.18 * (white - lp); band += 0.55 * ((white - lp) - band);
        double tone = (std::sin(TAU * phase) + 0.45 * std::sin(TAU * phase * 1.83)) * std::exp(-t * 30);
        double s = tone * 0.5 + band * 0.85 * std::exp(-t * (24 - 7 * vel));
        b.add(i0 + i, s * smooth(i / 20.0) * smooth((n - i) / 300.0) * vel, p.l, p.r);
    }
}

void hat(Bus &b, Rng &g, double t0, double vel, bool open, double pan = 0.3) {
    int n = int((open ? 0.42 : 0.07) * SR); long i0 = std::lround(t0 * SR); double a = 0, c = 0; Pan p(pan);
    for (int i = 0; i < n; i++) {
        double t = double(i) / SR, white = g.bi(); a += 0.62 * (white - a); c += 0.62 * ((white - a) - c);
        double s = ((white - a) - c) * std::exp(-t * (open ? 9 : 70));
        b.add(i0 + i, s * smooth(i / 12.0) * smooth((n - i) / 200.0) * vel, p.l, p.r);
    }
}

void tom(Bus &b, Rng &g, double t0, double midi, double vel, double pan) {
    double f = hz(midi), phase = 0, lp = 0; int n = int(0.45 * SR); long i0 = std::lround(t0 * SR); Pan p(pan);
    for (int i = 0; i < n; i++) {
        double t = double(i) / SR; phase += f * (1 + 0.35 * std::exp(-t / 0.04)) / SR; lp += 0.25 * (g.bi() - lp);
        double s = std::sin(TAU * phase) * std::exp(-t * 9) + lp * 0.3 * std::exp(-t * 60);
        b.add(i0 + i, s * smooth(i / 25.0) * smooth((n - i) / 300.0) * vel, p.l, p.r);
    }
}

// A crash, or a swell that grows for `rise` seconds and peaks at t0.
void cymbal(Bus &b, Rng &g, double t0, double vel, double rise = 0, double pan = -0.25) {
    const int partials = 30; double w[partials], phase[partials], decay[partials];
    for (int k = 0; k < partials; k++) { double f = 340 * std::pow(2.0, g.uni() * 4.7); w[k] = TAU * f / SR; phase[k] = g.uni() * TAU; decay[k] = 1.6 + f / 2600; }
    int before = int(rise * SR), n = before + int(3.2 * SR); long i0 = std::lround(t0 * SR) - before; double a = 0; Pan p(pan);
    for (int i = 0; i < n; i++) {
        double t = double(i - before) / SR, white = g.bi(), metal = 0; a += 0.45 * (white - a);
        double grow = t < 0 ? std::pow(double(i) / before, 2.6) : 1;
        for (int k = 0; k < partials; k++) metal += std::sin(phase[k] + w[k] * i) * (t < 0 ? 1 : std::exp(-t * decay[k]));
        double hiss = (white - a) * (t < 0 ? 1 : 0.45 * std::exp(-t * 3) + 0.55 * std::exp(-t * 14));
        b.add(i0 + i, (metal * 0.09 + hiss * 0.5) * grow * smooth(i / 60.0) * smooth((n - i) / 4000.0) * vel, p.l, p.r);
    }
}

// ---------------------------------------------------------------- hall and master
// An eight-line feedback delay network with damping; linear, so loop tails can be folded.
void hall(std::vector<float> &l, std::vector<float> &r, double seconds) {
    static const int length[8] = {1557, 1811, 2053, 2281, 2543, 2797, 3089, 3323};
    static const int diffuser[4] = {151, 373, 113, 281};
    std::vector<float> line[8]; int at[8] = {}; double gain[8], damp[8] = {};
    for (int k = 0; k < 8; k++) { line[k].assign(length[k], 0); gain[k] = std::pow(10.0, -3.0 * length[k] / (SR * seconds)); }
    std::vector<float> ap[4]; int apAt[4] = {};
    for (int k = 0; k < 4; k++) ap[k].assign(diffuser[k], 0);
    const int predelay = int(0.022 * SR); double lowL = 0, lowR = 0;
    std::vector<float> inL(l), inR(r);
    for (size_t i = 0; i < l.size(); i++) {
        double x[2] = {i >= size_t(predelay) ? inL[i - predelay] : 0.0, i >= size_t(predelay) ? inR[i - predelay] : 0.0};
        for (int k = 0; k < 4; k++) {
            double &s = x[k / 2], delayed = ap[k][apAt[k]], input = s + 0.6 * delayed;
            ap[k][apAt[k]] = float(input); apAt[k] = (apAt[k] + 1) % diffuser[k]; s = delayed - 0.6 * input;
        }
        double out[8], sum = 0;
        for (int k = 0; k < 8; k++) { out[k] = line[k][at[k]]; sum += out[k]; }
        for (int k = 0; k < 8; k++) {
            double feedback = (out[k] - sum * 0.25) * gain[k];
            damp[k] += 0.42 * (feedback - damp[k]);
            line[k][at[k]] = float(damp[k] + x[k % 2] * 0.5); at[k] = (at[k] + 1) % length[k];
        }
        double wetL = out[0] - out[2] + out[4] - out[6], wetR = out[1] - out[3] + out[5] - out[7];
        lowL += 0.02 * (wetL - lowL); lowR += 0.02 * (wetR - lowR);
        l[i] = float((wetL - lowL) * 0.5); r[i] = float((wetR - lowR) * 0.5);
    }
}

struct Mix {
    double bpm; int beats; double tail; Rng g; int n, total; bool loop;
    std::vector<std::unique_ptr<Bus>> buses;
    Mix(double bpm, int beats, double tail, bool loop, uint32_t seed) : bpm(bpm), beats(beats), tail(tail), g{seed}, loop(loop) {
        n = int(std::lround(beats * 60.0 / bpm * SR)); total = n + int(tail * SR);
    }
    Bus &bus(const std::string &name, double gain, double send) {
        buses.push_back(std::make_unique<Bus>(Bus{name, gain, send, std::vector<float>(total), std::vector<float>(total)}));
        return *buses.back();
    }
    double at(double beat) { return beat * 60.0 / bpm + g.bi() * 0.003; }
    double exact(double beat) const { return beat * 60.0 / bpm; }
    double span(double beats) const { return beats * 60.0 / bpm; }
};

struct Audio { std::vector<float> l, r; };

void writeWav(const std::string &path, const Audio &a) {
    std::ofstream out(path, std::ios::binary);
    auto u16 = [&](uint16_t v) { out.put(char(v)); out.put(char(v >> 8)); };
    auto u32 = [&](uint32_t v) { u16(v & 65535); u16(v >> 16); };
    uint32_t size = uint32_t(a.l.size() * 4);
    out.write("RIFF", 4); u32(size + 36); out.write("WAVEfmt ", 8); u32(16);
    u16(1); u16(2); u32(SR); u32(SR * 4); u16(4); u16(16); out.write("data", 4); u32(size);
    for (size_t i = 0; i < a.l.size(); i++) {
        u16(uint16_t(int16_t(std::lround(std::clamp(a.l[i], -1.0f, 1.0f) * 32767))));
        u16(uint16_t(int16_t(std::lround(std::clamp(a.r[i], -1.0f, 1.0f) * 32767))));
    }
}

// Mixes the buses, adds the hall and folds a loop's tail onto its beginning.
// `gain` <= 0 normalizes to `targetRms` and returns the gain, so a bridge can share it.
Audio master(Mix &m, double wet, double hallSeconds, double targetRms, double &gain, const std::string &stems = "") {
    std::vector<float> l(m.total), r(m.total), sl(m.total), sr(m.total);
    for (auto &b : m.buses) {
        double energy = 0;
        for (int i = 0; i < m.total; i++) {
            float x = float(b->l[i] * b->gain), y = float(b->r[i] * b->gain);
            l[i] += x; r[i] += y; sl[i] += float(x * b->send); sr[i] += float(y * b->send); energy += x * x + y * y;
        }
        if (!stems.empty()) {
            std::cout << stems << " · " << b->name << " " << 10 * std::log10(energy / (2.0 * m.n) + 1e-12) << " dB\n";
            Audio stem{std::vector<float>(m.total), std::vector<float>(m.total)};
            for (int i = 0; i < m.total; i++) { stem.l[i] = float(b->l[i] * b->gain); stem.r[i] = float(b->r[i] * b->gain); }
            writeWav(stems + "-" + b->name + ".wav", stem);
        }
    }
    hall(sl, sr, hallSeconds);
    if (!stems.empty()) {
        double dry = 0, tail = 0;
        for (int i = 0; i < m.total; i++) { dry += l[i] * l[i] + r[i] * r[i]; tail += sl[i] * sl[i] + sr[i] * sr[i]; }
        std::cout << stems << " · hall " << 10 * std::log10(wet * wet * tail / dry) << " dB below the dry mix\n";
    }
    double hpL = 0, hpR = 0;
    for (int i = 0; i < m.total; i++) {
        double x = l[i] + wet * sl[i], y = r[i] + wet * sr[i];
        hpL += 0.004 * (x - hpL); hpR += 0.004 * (y - hpR);
        l[i] = float(x - hpL); r[i] = float(y - hpR);
    }
    int n = m.loop ? m.n : m.total;
    Audio a{std::vector<float>(n), std::vector<float>(n)};
    for (int i = 0; i < n; i++) { a.l[i] = l[i]; a.r[i] = r[i]; }
    if (m.loop) for (int i = m.n; i < m.total; i++) { a.l[i - m.n] += l[i]; a.r[i - m.n] += r[i]; }
    else for (int i = 0; i < n; i++) { float fade = float(smooth((n - i) / (0.6 * SR))); a.l[i] *= fade; a.r[i] *= fade; }
    if (gain <= 0) {
        double energy = 0;
        for (int i = 0; i < n; i++) energy += a.l[i] * a.l[i] + a.r[i] * a.r[i];
        gain = targetRms / std::sqrt(energy / (2.0 * n));
    }
    // Look-ahead limiter; circular on loops so the seam stays continuous.
    const int window = 220; const double ceiling = 0.89;
    std::vector<float> need(n), low(n);
    for (int i = 0; i < n; i++) {
        double peak = std::max(std::abs(a.l[i]), std::abs(a.r[i])) * gain;
        need[i] = float(peak > ceiling ? ceiling / peak : 1.0);
    }
    auto wrap = [&](int i) { return m.loop ? (i % n + n) % n : std::clamp(i, 0, n - 1); };
    for (int i = 0; i < n; i++) {
        float least = 1;
        for (int k = 0; k <= window; k++) least = std::min(least, need[wrap(i + k)]);
        low[i] = least;
    }
    double sum = 0;
    for (int k = 0; k < window; k++) sum += low[wrap(k - window)];
    for (int i = 0; i < n; i++) {
        sum += low[i] - low[wrap(i - window)];
        double g = gain * sum / window;
        a.l[i] = float(a.l[i] * g); a.r[i] = float(a.r[i] * g);
    }
    return a;
}

// ---------------------------------------------------------------- notation
struct Note { double beat, len; int pitch; };
std::vector<Note> parse(const std::string &text, double start = 0) {
    std::istringstream in(text); std::string token; std::vector<Note> notes; double t = start;
    while (in >> token) {
        if (token == "|") continue;
        auto colon = token.find(':'); std::string name = token.substr(0, colon); double len = std::stod(token.substr(colon + 1));
        if (name != "r") {
            static const int base[7] = {9, 11, 0, 2, 4, 5, 7};
            int pc = base[name[0] - 'a']; size_t i = 1;
            if (name[i] == '#') { pc++; i++; } else if (name[i] == 'b') { pc--; i++; }
            notes.push_back({t, len, 12 * (name[i] - '0' + 1) + pc});
        }
        t += len;
    }
    return notes;
}
struct Chord { int root, third, fifth; };
const Chord Dm{2, 5, 9}, Bb{10, 2, 5}, C{0, 4, 7}, Gm{7, 10, 2}, A{9, 1, 4}, F{5, 9, 0}, Am{9, 0, 4}, Asus{9, 2, 4};
int up(int pc, int from) { return from + ((pc - from) % 12 + 12) % 12; }

// ---------------------------------------------------------------- orchestra
struct Orchestra {
    Bus &violins, &ostinato, &pad, &bass, &low, &trumpets, &horns, &flute, &harp, &piano, &timpani, &kick, &snare, &hats, &cymbals;
    Orchestra(Mix &m) : violins(m.bus("violins", 0.20, 0.42)), ostinato(m.bus("ostinato", 0.24, 0.30)), pad(m.bus("pad", 0.065, 0.45)),
        bass(m.bus("bass", 0.18, 0.16)), low(m.bus("sub", 0.11, 0.0)), trumpets(m.bus("trumpets", 0.20, 0.36)), horns(m.bus("horns", 0.17, 0.45)),
        flute(m.bus("flute", 0.21, 0.42)), harp(m.bus("harp", 0.45, 0.35)), piano(m.bus("piano", 0.22, 0.25)), timpani(m.bus("timpani", 0.34, 0.35)),
        kick(m.bus("kick", 0.38, 0.05)), snare(m.bus("snare", 0.60, 0.22)), hats(m.bus("hats", 0.55, 0.12)), cymbals(m.bus("cymbals", 0.40, 0.30)) {}
};

enum Voice { Violins, Trumpets, Horns, Flute };
void line(Mix &m, Orchestra &o, Voice voice, const std::vector<Note> &notes, double vel, int transpose = 0, double pan = 0) {
    for (auto &note : notes) {
        double t = m.at(note.beat), d = m.span(note.len), v = vel * (0.9 + 0.1 * std::min(1.0, note.len) + m.g.bi() * 0.04);
        int pitch = note.pitch + transpose;
        if (voice == Violins) strings(o.violins, m.g, t, d * 1.02, pitch, v, pan - 0.3, false, 6, 1.5);
        else if (voice == Trumpets) brass(o.trumpets, m.g, t, d * 0.93, pitch, v, pan + 0.22, false);
        else if (voice == Horns) brass(o.horns, m.g, t, d * 0.97, pitch, v, pan - 0.2, true);
        else flute(o.flute, m.g, t, d * 0.98, pitch, v, pan + 0.1);
    }
}

void bassNote(Mix &m, Orchestra &o, double beat, double beats, int pitch, double vel, bool spiccato) {
    strings(o.bass, m.g, m.at(beat), m.span(beats), pitch, vel, 0.12, spiccato, 4, 0.9);
    strings(o.bass, m.g, m.at(beat), m.span(beats), pitch + 12, vel * 0.6, 0.2, spiccato, 3, 0.9);
    sub(o.low, m.exact(beat), spiccato ? std::min(m.span(beats), 0.15) : m.span(beats), pitch, vel);
}

void chordPad(Mix &m, Orchestra &o, double beat, double beats, const Chord &c, double vel, int low = 55) {
    int pcs[3] = {c.root, c.third, c.fifth};
    for (int k = 0; k < 3; k++) strings(o.pad, m.g, m.at(beat), m.span(beats), up(pcs[k], low), vel, (k - 1) * 0.4, false, 4, 0.8);
}

void hornPad(Mix &m, Orchestra &o, double beat, double beats, const Chord &c, double vel) {
    brass(o.horns, m.g, m.at(beat), m.span(beats) * 0.96, up(c.root, 48), vel, -0.35, true);
    brass(o.horns, m.g, m.at(beat), m.span(beats) * 0.96, up(c.fifth, 50), vel * 0.85, -0.1, true);
}

// Brass and timpani on the 3+3+2 accent that drives both battle pieces.
void stabs(Mix &m, Orchestra &o, double bar, const Chord &c, double vel) {
    int pcs[3] = {c.root, c.third, c.fifth}; double beats[3] = {0, 1.5, 3}, lens[3] = {0.9, 0.9, 0.7};
    for (int hit = 0; hit < 3; hit++) {
        for (int k = 0; k < 3; k++) brass(o.trumpets, m.g, m.at(bar + beats[hit]), m.span(lens[hit]), up(pcs[k], 62), vel * (hit ? 0.85 : 1), 0.1 + k * 0.15, false);
        brass(o.horns, m.g, m.at(bar + beats[hit]), m.span(lens[hit]), up(c.root, 45), vel, -0.3, true);
    }
    timpani(o.timpani, m.g, m.exact(bar), up(c.root, 41), vel);
    timpani(o.timpani, m.g, m.exact(bar + 3), up(c.root, 41), vel * 0.8);
}

void snareRoll(Mix &m, Orchestra &o, double from, double to, double step, double start, double end) {
    for (double beat = from; beat < to - 1e-6; beat += step) {
        double x = (beat - from) / (to - from);
        snare(o.snare, m.g, m.exact(beat), start + (end - start) * x * x);
    }
}

// Spiccato figure: root, fifth, octave and tenth, accented 3+3+2.
void figure(Mix &m, Orchestra &o, double bar, const Chord &c, double vel, bool sixteenths, int octave = 0) {
    int root = up(c.root, 48) + octave, fifth = up(c.fifth, root), top = root + 12, tenth = up(c.third, top);
    int order[8] = {root, fifth, top, fifth, tenth, fifth, top, fifth};
    for (int step = 0; step < 8; step++) {
        bool accent = step == 0 || step == 3 || step == 6;
        double v = vel * (accent ? 1.0 : 0.62) * (1 + m.g.bi() * 0.06);
        strings(o.ostinato, m.g, m.at(bar + step * 0.5), m.span(0.5), order[step], v, 0.3, true, 5);
        if (sixteenths) strings(o.ostinato, m.g, m.at(bar + step * 0.5 + 0.25), m.span(0.25), order[step], v * 0.6, 0.3, true, 5);
    }
}

// Drum kit bar. style 0: driving, 1: half-time, 2: four on the floor, 3: driving with sixteenth hats.
void kit(Mix &m, Orchestra &o, double bar, int style, double vel, bool fill) {
    if (style == 1) {
        kick(o.kick, m.g, m.exact(bar), vel, false); kick(o.kick, m.g, m.exact(bar + 2.5), vel * 0.8, false);
        snare(o.snare, m.g, m.exact(bar + 2), vel * 0.8);
        for (int q = 0; q < 4; q++) hat(o.hats, m.g, m.exact(bar + q), vel * 0.8, false);
    } else {
        if (style == 2) for (int q = 0; q < 4; q++) kick(o.kick, m.g, m.exact(bar + q), vel, false);
        else for (double beat : {0.0, 1.5, 2.5}) kick(o.kick, m.g, m.exact(bar + beat), vel * (beat == 0 ? 1 : 0.85), false);
        if (!fill) { snare(o.snare, m.g, m.exact(bar + 1), vel); snare(o.snare, m.g, m.exact(bar + 3), vel); }
        else snare(o.snare, m.g, m.exact(bar + 1), vel);
        int count = style == 3 ? 16 : 8;
        for (int step = 0; step < count; step++) {
            double beat = step * 4.0 / count; bool on = std::fmod(beat, 1.0) == 0;
            if (style != 3 && step == 7 && !fill) hat(o.hats, m.g, m.exact(bar + beat), vel * 0.7, true);
            else hat(o.hats, m.g, m.exact(bar + beat), vel * (on ? 1.0 : std::fmod(beat, 0.5) == 0 ? 0.7 : 0.45), false);
        }
    }
    if (fill) {
        double hits[6] = {2.5, 2.75, 3, 3.25, 3.5, 3.75}; int drums[6] = {-1, -1, 50, 50, 45, 41};
        for (int k = 0; k < 6; k++) {
            if (drums[k] < 0) snare(o.snare, m.g, m.exact(bar + hits[k]), vel * (0.6 + 0.08 * k));
            else tom(o.kick, m.g, m.exact(bar + hits[k]), drums[k], vel * 0.55, (k - 3.5) * 0.25);
        }
    }
}

// ---------------------------------------------------------------- compositions
const char *themeHead = "d5:1.5 f5:1.5 a5:1 | a5:1 g5:.5 f5:.5 e5:.5 f5:.5 d5:1 | d5:1.5 f5:1.5 bb5:1 | a5:1 g5:.5 e5:.5 g5:2 | ";
const std::string themeOpen = std::string(themeHead) + "a5:1.5 d6:1.5 c6:1 | bb5:1.5 a5:.5 g5:1 f5:1 | g5:1.5 bb5:1.5 g5:1 | a5:1.5 e5:1.5 c#5:1";
const std::string themeClosed = std::string(themeHead) + "f5:1.5 a5:1.5 c6:1 | d6:1 c6:.5 bb5:.5 a5:1 g5:1 | a5:1.5 c#6:1.5 e6:1 | d6:3 r:1";

// "Apuesta de acero": 160 BPM, D minor, 48 bars. Vamp, theme twice, a lyrical
// middle, a rising build, the theme in full and a turnaround into the vamp.
void combat(Mix &m, Orchestra &o) {
    const Chord chords[48] = {Dm, Dm, Bb, C, Dm, Dm, Bb, C, Dm, Bb, Gm, A, Dm, Dm, Bb, C, F, Gm, A, Dm,
        Bb, C, Am, Dm, Bb, C, F, A, Gm, Gm, A, A, Bb, C, Asus, A, Dm, Dm, Bb, C, F, Gm, A, Dm, Gm, Bb, A, A};
    enum { Vamp, First, Second, Middle, Build, Full, Turn };
    auto section = [](int bar) { return bar < 4 ? Vamp : bar < 12 ? First : bar < 20 ? Second : bar < 28 ? Middle : bar < 36 ? Build : bar < 44 ? Full : Turn; };
    for (int bar = 0; bar < 48; bar++) {
        const Chord &c = chords[bar]; int part = section(bar); double beat = bar * 4;
        int root = up(c.root, 33); bool last = bar == 11 || bar == 19 || bar == 43 || bar == 47;
        if (part == Middle) {
            bassNote(m, o, beat, 2.8, root, 0.7, false);
            bassNote(m, o, beat + 3, 0.5, root, 0.6, true); bassNote(m, o, beat + 3.5, 0.5, root + 7, 0.6, true);
            int r = up(c.root, 50), fifth = up(c.fifth, r), top = r + 12, tenth = up(c.third, top);
            int order[8] = {r, fifth, top, tenth, top + 7, tenth, top, fifth};
            for (int step = 0; step < 8; step++) pluck(o.harp, m.g, m.at(beat + step * 0.5), order[step], 0.55 + (step % 4 == 0 ? 0.2 : 0), -0.35 + step * 0.08, 2.4, 0.3);
            chordPad(m, o, beat, 4, c, 0.6);
            kit(m, o, beat, 1, 0.75, bar == 27);
        } else {
            double grow = part == Build ? 0.6 + 0.4 * (bar - 28) / 7.0 : 1;
            figure(m, o, beat, c, (part == Vamp ? 0.85 : 0.75) * grow, false);
            if (part == Full) figure(m, o, beat, c, 0.45, false, 12);
            for (int step = 0; step < 8; step++) {
                bool accent = step == 0 || step == 3 || step == 6;
                bassNote(m, o, beat + step * 0.5, 0.5, root + (step == 3 || step == 6 ? 12 : 0), (accent ? 0.9 : 0.6) * grow, true);
            }
            for (double hit : {0.0, 1.5, 3.0}) {
                piano(o.piano, m.g, m.at(beat + hit), m.span(1.0), root, 0.75 * grow, -0.1);
                piano(o.piano, m.g, m.at(beat + hit), m.span(1.0), root + 12, 0.7 * grow, 0.0);
            }
            if (part == Build) {
                chordPad(m, o, beat, 4, c, 0.5 + 0.4 * (bar - 28) / 7.0, 60);
                stabs(m, o, beat, c, 0.55 + 0.35 * (bar - 28) / 7.0);
                kit(m, o, beat, 2, 0.7 + 0.2 * (bar - 28) / 7.0, false);
            } else if (part == Turn) {
                stabs(m, o, beat, c, 0.85);
                kit(m, o, beat, 0, 0.9, last);
            } else {
                if (part != Vamp) hornPad(m, o, beat, 4, c, part == Full ? 0.7 : 0.55);
                if (part == Second || part == Full) chordPad(m, o, beat, 4, c, part == Full ? 0.7 : 0.55);
                timpani(o.timpani, m.g, m.exact(beat), up(c.root, 41), part == Full ? 0.8 : 0.6);
                kit(m, o, beat, 0, part == Full ? 0.95 : 0.85, last);
            }
        }
    }
    // The vamp's low riff states the rhythm before the trumpets take the theme.
    auto riff = parse("d3:1.5 d3:1.5 f3:1 | d3:1.5 a3:1.5 g3:.5 f3:.5 | bb2:1.5 bb2:1.5 d3:1 | c3:1.5 e3:1.5 g3:1");
    line(m, o, Horns, riff, 0.8);
    for (auto &note : riff) strings(o.bass, m.g, m.at(note.beat), m.span(note.len) * 0.9, note.pitch, 0.7, 0.2, false, 4);
    line(m, o, Trumpets, parse(themeOpen, 16), 0.82);
    line(m, o, Trumpets, parse(themeClosed, 48), 0.86);
    line(m, o, Violins, parse(themeClosed, 48), 0.7);
    auto middle = parse("f5:2 d5:1 f5:1 | g5:2 e5:1 g5:1 | a5:2 g5:1 e5:1 | f5:3 d5:.5 e5:.5 | f5:2 bb5:1 a5:1 | g5:2 c6:1 bb5:1 | a5:3 f5:1 | e5:1 a5:1 c#6:2", 80);
    line(m, o, Flute, middle, 0.85);
    line(m, o, Violins, middle, 0.6, -12);
    line(m, o, Violins, parse("g5:4 | bb5:4 | a5:4 | c#6:4 | d6:4 | e6:4 | d6:4 | c#6:2 e6:2", 112), 0.8);
    line(m, o, Trumpets, parse(themeClosed, 144), 0.92);
    line(m, o, Violins, parse(themeClosed, 144), 0.85, 12);
    line(m, o, Horns, parse(themeClosed, 144), 0.6, -12);
    // Turnaround: violins fall through the dominant, then run back up to the tonic.
    line(m, o, Violins, parse("d6:2 bb5:2 | d6:2 f6:2 | e6:2 c#6:2 | a5:1 e5:1", 176), 0.8);
    auto run = parse("a4:.25 b4:.25 c#5:.25 d5:.25 e5:.25 f5:.25 g5:.25 a5:.25", 190);
    for (auto &note : run) strings(o.violins, m.g, m.at(note.beat), m.span(0.25), note.pitch, 0.8, -0.3, true, 6, 1.2);
    for (int bar : {0, 4, 12, 20, 36}) cymbal(o.cymbals, m.g, m.exact(bar * 4), bar == 20 ? 0.5 : 0.85);
    cymbal(o.cymbals, m.g, m.exact(28 * 4), 0.6);
    cymbal(o.cymbals, m.g, m.exact(36 * 4), 0.7, m.span(8));
    cymbal(o.cymbals, m.g, m.exact(48 * 4), 0.6, m.span(6));
    snareRoll(m, o, 34 * 4, 36 * 4, 0.25, 0.2, 0.9);
    for (double beat = 140; beat < 144; beat += 0.5) timpani(o.timpani, m.g, m.exact(beat), 45, 0.4 + 0.1 * (beat - 140));
}

// "La última tirada": 168 BPM, D minor, 40 bars. A descending cadence, a soaring
// answer, the combat theme recalled in full, a held breath and a last statement.
void climax(Mix &m, Orchestra &o) {
    const Chord chords[40] = {Dm, C, Bb, A, Dm, C, Bb, A, Gm, A, Dm, Bb, Gm, A, Dm, A, Dm, Dm, Bb, C, F, Gm, A, Dm,
        Dm, Dm, Bb, Bb, Gm, Gm, A, A, Bb, C, Dm, Dm, Bb, C, A, A};
    for (int bar = 0; bar < 40; bar++) {
        const Chord &c = chords[bar]; int part = bar / 8; double beat = bar * 4; int root = up(c.root, 33);
        bool last = bar % 8 == 7 && part != 3;
        if (part == 3) {
            // Held breath: low strings, timpani and a slow build.
            double grow = 0.45 + 0.55 * (bar - 24) / 7.0;
            for (int step = 0; step < 8; step++) {
                bool accent = step == 0 || step == 3 || step == 6;
                bassNote(m, o, beat + step * 0.5, 0.5, root, (accent ? 0.8 : 0.45) * grow, true);
                if (accent) timpani(o.timpani, m.g, m.exact(beat + step * 0.5), up(c.root, 41), 0.75 * grow);
            }
            chordPad(m, o, beat, 4, c, 0.45 + 0.4 * (bar - 24) / 7.0);
            if (bar >= 28) figure(m, o, beat, c, 0.5 * grow, true);
            kick(o.kick, m.g, m.exact(beat), 0.8, true);
            continue;
        }
        figure(m, o, beat, c, 0.8, part != 1);
        if (part != 1) figure(m, o, beat, c, 0.4, false, 12);
        for (int step = 0; step < 8; step++) {
            bool accent = step == 0 || step == 3 || step == 6;
            bassNote(m, o, beat + step * 0.5, 0.5, root + (step == 3 || step == 6 ? 12 : 0), accent ? 0.95 : 0.65, true);
        }
        for (double hit : {0.0, 1.5, 3.0}) {
            piano(o.piano, m.g, m.at(beat + hit), m.span(1.0), root, 0.8, -0.1);
            piano(o.piano, m.g, m.at(beat + hit), m.span(1.0), root + 12, 0.75, 0.0);
        }
        chordPad(m, o, beat, 4, c, 0.65, part == 1 ? 55 : 60);
        hornPad(m, o, beat, 4, c, 0.65);
        timpani(o.timpani, m.g, m.exact(beat), up(c.root, 41), 0.85);
        if (part != 1) timpani(o.timpani, m.g, m.exact(beat + 3), up(c.root, 41), 0.6);
        kit(m, o, beat, part == 1 ? 2 : 3, 0.95, last);
    }
    auto cadence = parse("d5:1 a4:.5 d5:.5 f5:1 e5:.5 d5:.5 | e5:1 c5:.5 e5:.5 g5:2 | f5:1 d5:.5 f5:.5 bb5:1 a5:.5 g5:.5 | a5:1.5 g5:.5 e5:1 c#5:1 | "
        "d5:1 a4:.5 d5:.5 f5:1 g5:.5 a5:.5 | g5:1 e5:.5 g5:.5 c6:2 | d6:1 bb5:.5 d6:.5 c6:1 bb5:.5 g5:.5 | a5:1 c#6:1 e6:2");
    line(m, o, Trumpets, cadence, 0.9);
    line(m, o, Violins, cadence, 0.8, 12);
    auto answer = parse("d6:2 bb5:1 g5:1 | a5:1.5 bb5:.5 a5:1 e5:1 | f5:2 a5:1 d6:1 | d6:1.5 c6:.5 bb5:2 | bb5:2 g5:1 bb5:1 | a5:2 g5:1 e5:1 | "
        "f5:1 e5:.5 d5:.5 a5:2 | a5:.5 b5:.5 c#6:.5 d6:.5 e6:2", 32);
    line(m, o, Violins, answer, 0.88);
    line(m, o, Horns, answer, 0.7, -12);
    line(m, o, Trumpets, parse(themeClosed, 64), 0.95);
    line(m, o, Violins, parse(themeClosed, 64), 0.85, 12);
    line(m, o, Horns, parse(themeClosed, 64), 0.65, -12);
    auto breath = parse("a5:2 f5:1 e5:1 | d5:3 r:1 | bb5:2 f5:1 d5:1 | f5:3 r:1 | bb5:2 a5:1 g5:1 | d6:4 | c#6:2 e6:2 | e6:4", 96);
    line(m, o, Flute, breath, 0.9);
    line(m, o, Violins, breath, 0.5, -12);
    auto final = parse("f5:1 d5:.5 f5:.5 bb5:1 a5:.5 g5:.5 | g5:1 e5:.5 g5:.5 c6:2 | d6:1 a5:.5 d6:.5 f6:1 e6:.5 d6:.5 | d6:2 a5:1 f5:1 | "
        "f5:1 d5:.5 f5:.5 bb5:1 a5:.5 g5:.5 | g5:1 e5:.5 g5:.5 c6:1 e6:1 | e6:1.5 c#6:1.5 a5:1 | a5:1.5 e5:.5 a5:1 c#6:1", 128);
    line(m, o, Trumpets, final, 0.95);
    line(m, o, Violins, final, 0.85, 12);
    for (int bar : {0, 8, 16, 32}) cymbal(o.cymbals, m.g, m.exact(bar * 4), 0.9);
    cymbal(o.cymbals, m.g, m.exact(4 * 4), 0.5); cymbal(o.cymbals, m.g, m.exact(36 * 4), 0.5);
    cymbal(o.cymbals, m.g, m.exact(32 * 4), 0.7, m.span(8));
    cymbal(o.cymbals, m.g, m.exact(40 * 4), 0.6, m.span(4));
    snareRoll(m, o, 30 * 4, 32 * 4, 0.25, 0.2, 0.95);
}

// Lead-in played once before the combat theme: a dominant swell and a rising fanfare.
void toCombat(Mix &m, Orchestra &o) {
    kick(o.kick, m.g, 0, 0.85, true);
    piano(o.piano, m.g, 0, m.span(6), 33, 0.8, 0); piano(o.piano, m.g, 0, m.span(6), 45, 0.7, 0);
    // A harp sweep up the dominant covers the menu's fade.
    int sweep[12] = {45, 52, 57, 61, 64, 67, 69, 73, 76, 79, 81, 85};
    for (int k = 0; k < 12; k++) pluck(o.harp, m.g, m.exact(k * 0.17), sweep[k], 0.6 + 0.03 * k, -0.5 + 0.09 * k, 3.0, 0.25);
    cymbal(o.cymbals, m.g, m.exact(8), 0.85, m.span(7.5));
    for (int step = 0; step < 32; step++) timpani(o.timpani, m.g, m.exact(step * 0.25), 45, 0.25 + 0.62 * std::pow(step / 31.0, 1.6));
    int tones[4] = {57, 61, 64, 67};
    for (int step = 0; step < 16; step++) for (int k = 0; k < 4; k++)
        strings(o.pad, m.g, m.at(step * 0.5), m.span(0.5), tones[k] + (step >= 8 ? 12 : 0), 0.4 + 0.55 * step / 15.0, (k - 1.5) * 0.3, true, 4);
    bassNote(m, o, 0, 8, 33, 0.5, false);
    auto fanfare = parse("e4:1 a4:1 c#5:.5 e5:.5 a5:1", 4);
    line(m, o, Trumpets, fanfare, 0.9); line(m, o, Horns, fanfare, 0.75, -12);
    snareRoll(m, o, 4, 7, 0.25, 0.15, 0.7); snareRoll(m, o, 7, 8, 0.125, 0.5, 0.95);
}

// Lead-in played once before the climax: three hammer blows, then a run up the dominant.
void toClimax(Mix &m, Orchestra &o) {
    double hits[3] = {0, 1.5, 3}; const Chord blow[3] = {Bb, Bb, Gm};
    for (int k = 0; k < 3; k++) {
        const Chord &c = blow[k]; int pcs[3] = {c.root, c.third, c.fifth};
        for (int v = 0; v < 3; v++) {
            brass(o.trumpets, m.g, m.exact(hits[k]), m.span(1.1), up(pcs[v], 62), 0.8, 0.1 + v * 0.15, false);
            strings(o.pad, m.g, m.exact(hits[k]), m.span(1.2), up(pcs[v], 55), 0.8, (v - 1) * 0.4, false, 4);
        }
        brass(o.horns, m.g, m.exact(hits[k]), m.span(1.1), up(c.root, 45), 0.75, -0.3, true);
        timpani(o.timpani, m.g, m.exact(hits[k]), up(c.root, 41), 0.8);
        kick(o.kick, m.g, m.exact(hits[k]), 0.7, true);
        bassNote(m, o, hits[k], 1.2, 34, 0.6, false);
        piano(o.piano, m.g, m.exact(hits[k]), m.span(1.2), 34, 0.8, 0);
    }
    cymbal(o.cymbals, m.g, 0, 0.9);
    int run[16] = {69, 70, 73, 74, 76, 77, 79, 81, 82, 81, 79, 81, 82, 85, 88, 85};
    for (int k = 0; k < 16; k++) {
        strings(o.violins, m.g, m.at(4 + k * 0.25), m.span(0.25), run[k], 0.55 + 0.4 * k / 15.0, -0.3, true, 6, 1.2);
        strings(o.ostinato, m.g, m.at(4 + k * 0.25), m.span(0.25), run[k] - 12, 0.5 + 0.4 * k / 15.0, 0.3, true, 5);
    }
    for (int pitch : {57, 61, 64, 69}) brass(o.horns, m.g, m.exact(4), m.span(3.9), pitch, 0.8, (pitch - 62) * 0.05, pitch < 64);
    bassNote(m, o, 4, 4, 33, 0.6, false);
    for (int step = 0; step < 16; step++) timpani(o.timpani, m.g, m.exact(4 + step * 0.25), 45, 0.3 + 0.6 * step / 15.0);
    snareRoll(m, o, 4, 8, 0.25, 0.25, 0.95);
    cymbal(o.cymbals, m.g, m.exact(8), 0.85, m.span(4));
}

// "Antes del destino": a 3/4 waltz in D minor at 126 BPM, 48 bars, for guitar,
// flute and strings.
void menu(Mix &m) {
    Bus &guitar = m.bus("guitar", 0.60, 0.30), &fl = m.bus("flute", 0.20, 0.50), &violins = m.bus("violins", 0.15, 0.50),
        &pad = m.bus("pad", 0.055, 0.55), &bass = m.bus("bass", 0.65, 0.20), &bells = m.bus("bells", 0.30, 0.55), &harp = m.bus("harp", 0.36, 0.45);
    const Chord chords[48] = {Dm, Dm, Bb, Bb, F, F, C, C, Dm, Dm, Bb, Bb, Gm, Gm, A, A, F, F, C, C, Dm, Dm, Am, Am,
        Bb, Bb, F, F, Gm, Gm, A, A, Dm, Dm, Bb, Bb, F, F, C, C, Gm, Gm, Dm, Dm, Bb, A, Dm, A};
    for (int bar = 0; bar < 48; bar++) {
        const Chord &c = chords[bar]; double beat = bar * 3; int part = bar / 16;
        int root = up(c.root, 45), fifth = root + 7, top = root + 12, tenth = up(c.third, top);
        int rise[6] = {root, fifth, tenth, fifth, top, fifth}, wave[6] = {root, fifth, top, tenth, top, fifth};
        for (int step = 0; step < 6; step++) {
            double v = (step == 0 ? 0.8 : step % 2 ? 0.48 : 0.6) * (1 + m.g.bi() * 0.08);
            pluck(guitar, m.g, m.at(beat + step * 0.5), (part == 1 ? wave : rise)[step], v, -0.25 + 0.06 * step, 2.6, 0.3);
        }
        pluck(bass, m.g, m.at(beat), up(c.root, 33), 0.7, 0.05, 1.4, 0.75);
        if (part == 1) pluck(bass, m.g, m.at(beat + 2), up(c.fifth, 36), 0.45, 0.05, 1.0, 0.75);
        if (bar >= 8) {
            int pcs[3] = {c.root, c.third, c.fifth}; double v = part == 1 ? 0.6 : 0.45;
            for (int k = 0; k < 3; k++) strings(pad, m.g, m.at(beat), m.span(3), up(pcs[k], 55), v, (k - 1) * 0.45, false, 4, 0.7);
        }
        if (bar % 16 == 15) {
            int pcs[3] = {c.root, c.third, c.fifth}, pitch = up(c.root, 57);
            for (int step = 0; step < 6; step++) {
                pluck(harp, m.g, m.at(beat + step * 0.5), pitch, 0.5 + 0.06 * step, 0.4 - 0.1 * step, 3.0, 0.25);
                pitch = up(pcs[(step + 1) % 3], pitch + 1);
            }
        }
    }
    auto play = [&](Bus &b, int kind, const std::vector<Note> &notes, double vel, int transpose) {
        for (auto &note : notes) {
            double t = m.at(note.beat), d = m.span(note.len), v = vel * (0.92 + m.g.bi() * 0.05);
            if (kind == 0) flute(b, m.g, t, d * 0.97, note.pitch + transpose, v, 0.15);
            else if (kind == 1) strings(b, m.g, t, d * 1.03, note.pitch + transpose, v, -0.25, false, 6, 1.0);
            else if (note.len >= 2) bell(b, t, note.pitch + transpose, v, 0.35);
        }
    };
    const std::string opening = "a4:2 d5:1 | f5:2 e5:1 | d5:2 bb4:1 | d5:3 | c5:2 f5:1 | a5:2 g5:1 | e5:2 c5:1 | e5:3 | ";
    auto first = parse(opening + "d5:1 f5:1 a5:1 | d6:2 c6:1 | bb5:2 a5:1 | f5:3 | g5:2 bb5:1 | bb5:1 a5:1 g5:1 | e5:1 d5:1 c#5:1 | e5:3");
    play(fl, 0, first, 0.8, 0);
    auto second = parse("a5:3 | c6:2 bb5:1 | g5:3 | e5:2 g5:1 | f5:3 | a5:2 g5:1 | e5:3 | c5:2 e5:1 | d5:2 f5:1 | bb5:3 | a5:2 f5:1 | c6:3 | "
        "bb5:2 g5:1 | d6:2 bb5:1 | a5:3 | c#6:2 a5:1", 48);
    play(violins, 1, second, 0.75, -12);
    std::vector<Note> echo(second.begin() + second.size() / 2, second.end());
    play(fl, 0, echo, 0.55, 0);
    auto third = parse(opening + "bb5:2 a5:1 | g5:2 d5:1 | f5:2 e5:1 | d5:3 | d5:1 f5:1 bb5:1 | a5:2 e5:1 | d5:3 | c#5:2 e5:1", 96);
    play(fl, 0, third, 0.82, 0);
    play(violins, 1, third, 0.5, -12);
    play(bells, 2, third, 0.6, 12);
}

// "Pulso de acero" and "La última orden" share D–E flat–A flat–A.
// The combat arrangement leaves room between accents. The climax presses the same motif forward.
void osamu(Mix &m, bool climax) {
    Orchestra o(m);
    Bus &metal = m.bus("metal", 0.12, 0.38);
    const Chord Eb{3, 7, 10}, Ab{8, 0, 3}, Ddim{2, 5, 8};
    const Chord main[] = {Dm, Ddim, Eb, Dm, Dm, Ab, Gm, A};
    const Chord middle[] = {Bb, Gm, Eb, Dm, Bb, Ab, Ddim, A};
    const std::string motif =
        "d4:1.5 eb4:.5 ab4:1 a4:.5 r:.5 | f4:1 d4:.75 eb4:.25 d4:1.5 r:.5 | "
        "eb4:1 g4:.5 bb4:.5 g4:1 f4:.5 eb4:.5 | d4:2 ab4:.5 a4:.5 d4:1 | "
        "d4:1.5 eb4:.5 ab4:1 a4:1 | c5:.75 bb4:.25 ab4:1 eb4:1 f4:1 | "
        "g4:1 d4:.5 g4:.5 bb4:1 ab4:.5 g4:.5 | a4:1 e4:.5 c#4:.5 a3:1 r:1";
    const std::string reply =
        "f4:2 d4:1 f4:1 | g4:1.5 f4:.5 d4:2 | eb4:1 f4:1 g4:.5 bb4:.5 g4:1 | "
        "ab4:.5 a4:.5 f4:1 d4:1 r:1 | bb3:1.5 d4:.5 f4:2 | eb4:2 ab4:1 c5:1 | "
        "d5:.75 c5:.25 ab4:1 f4:1 eb4:1 | e4:.5 a4:.5 c#5:1 bb4:.5 a4:1 r:.5";
    const auto bars = m.beats / 4;
    for (int i = 0; i < bars; ++i) {
        const double beat = i * 4.0;
        const int phrase = i / 8;
        const bool bridge = phrase == 2;
        const bool reprise = phrase >= 3;
        const auto &chord = (bridge ? middle : main)[i % 8];
        const int root = up(chord.root, 34);
        const int fifth = up(chord.fifth, root + 1);
        const int third = up(chord.third, root + 7);
        const int pulse[] = {root, root, fifth, root, root + 12, third, fifth, root};
        const bool sparse = !climax && (i < 4 || bridge);
        for (int step = 0; step < 8; ++step) {
            const bool accented = step == 0 || step == 3 || step == 6;
            const double noteBeat = beat + step * 0.5;
            if (!sparse || (step != 2 && step != 5 && step != 7))
                bassNote(m, o, noteBeat, 0.4, pulse[step], accented ? 0.78 : 0.42, true);
            if (i >= 4 || climax) {
                const int pitch = pulse[(step + 2) % 8] + 12;
                strings(o.ostinato, m.g, m.at(noteBeat + 0.03), m.span(0.3), pitch,
                    accented ? 0.73 : 0.55, 0.30, true, 4, climax ? 1.3 : 0.86);
                if (climax && !bridge && step != 7)
                    strings(o.ostinato, m.g, m.at(noteBeat + 0.25), m.span(0.18), third + 12 + (step % 2) * 12,
                        0.47, -0.35, true, 3, 1.1);
            }
        }
        if (!sparse || bridge) chordPad(m, o, beat, 3.85, chord, climax ? 0.69 : 0.56, 50);
        for (double offset : {0.0, 1.5, 3.0}) {
            if (!climax && offset > 0 && i < 4) continue;
            const double t = m.exact(beat + offset);
            if (offset == 0 || bridge || climax) timpani(o.timpani, m.g, t, root, offset == 0 ? 0.70 : 0.46);
            if (offset != 0 || i % 4 == 0) piano(o.piano, m.g, t, m.span(0.39), fifth + 12,
                offset == 1.5 ? 0.65 : 0.49, 0.16);
            if ((i + int(offset)) % 4 == 0) bell(metal, t, up(chord.fifth, 66), offset == 0 ? 0.55 : 0.35, -0.48);
            if ((climax && !bridge) || (reprise && i % 2 == 0))
                brass(o.horns, m.g, t, m.span(0.38), up(chord.root, 45), 0.67, -0.30, true);
        }
        if (i >= 4 || climax) kit(m, o, beat, climax ? (bridge ? 1 : 3) : (sparse ? 1 : 0),
            climax ? 0.70 : 0.53, i % 8 == 7);
        if (i % 8 == 7 && i + 1 < bars) cymbal(o.cymbals, m.g, m.exact(beat + 4), 0.47, m.span(1.5));
    }

    // Each eight-bar phrase changes the arrangement, then the final phrase returns to the opening pedal.
    for (int phrase = 0; phrase * 32 < m.beats; ++phrase) {
        if (!climax && phrase == 0) {
            const auto intro = parse("d3:1 eb3:.5 ab3:.5 a3:1 r:1 | r:4 | d3:1.5 eb3:.5 ab3:1 a3:1 | r:4", 8);
            line(m, o, Horns, intro, 0.85);
            continue;
        }
        const bool bridge = phrase == 2;
        const auto notes = parse(bridge ? reply : motif, phrase * 32);
        line(m, o, bridge ? Violins : Horns, notes, bridge ? 0.76 : 0.90, bridge ? 0 : -12);
        if (climax || phrase == 3) {
            line(m, o, Violins, notes, climax ? 0.78 : 0.64, 0, -0.1);
            if (phrase != 1 && !bridge) line(m, o, Trumpets, notes, climax ? 0.67 : 0.52, 12, 0.15);
        }
        if (bridge || !climax)
            for (const auto &note : notes) {
                if (note.len >= 1.0)
                    bell(metal, m.exact(note.beat), note.pitch + 24, bridge ? 0.50 : 0.30, 0.38);
            }
    }
}

// Ironwing, after 90s space-shooter scores: a heroic orchestral march with a trumpet
// fanfare, string ostinato and march snare. Combat is a major-key fanfare with bVI–bVII–I
// cadences; zero lives is a D minor theme over a flat-second riff.
const Chord D{2, 6, 9}, G{7, 11, 2}, Bm{11, 2, 6}, E{4, 8, 11}, Fsm{6, 9, 1}, Eb{3, 7, 10};

// Combat, 156 bpm, 40 bars: A, A', B (Lydian bridge), A' with counterline, fanfare tag.
const Chord heroA[] = {D, D, C, G, Bb, C, D, A};
const Chord heroA2[] = {D, D, C, G, Bb, C, D, D};
const Chord heroB[] = {Bm, G, A, Fsm, G, E, Bb, A};
const Chord heroTag[] = {G, A, Bb, C, D, D, Bb, C};
const char *heroHead =
    "d5:1 a5:1.5 g5:.25 f#5:.25 e5:1 | f#5:1.5 d5:.5 a4:2 | g5:1 e5:.5 c5:.5 g5:1.5 a5:.5 | b5:2 a5:.5 g5:.5 d5:1 | ";
const std::string heroThemeA = std::string(heroHead) +
    "f5:1 d5:.5 f5:.5 bb5:1.5 a5:.5 | g5:1 e5:.5 g5:.5 c6:1.5 b5:.25 c6:.25 | d6:2 a5:1 f#5:1 | e5:1 a5:.5 c#6:.5 e6:2";
const std::string heroThemeA2 = std::string(heroHead) +
    "d6:1 c6:.5 bb5:.5 f5:1 bb5:1 | c6:1.5 e6:.5 g6:2 | f#6:1 e6:.5 d6:.5 a5:1 f#5:1 | d6:3 r:1";
const std::string heroThemeB =
    "f#5:2 d5:1 b4:1 | g5:1.5 a5:.5 b5:1 c#6:1 | e6:2 c#6:1 a5:1 | c#6:1.5 a5:.5 f#5:2 | "
    "b5:1 d6:.5 b5:.5 g5:1 b5:1 | g#5:1.5 b5:.5 e6:2 | f6:1 d6:.5 bb5:.5 f5:1 d6:1 | c#6:1 e6:1 a6:1.5 r:.5";
const std::string heroThemeTag =
    "d6:1 b5:.5 g5:.5 d5:2 | e5:1 a5:.5 c#6:.5 e6:2 | f6:1.5 d6:.5 bb5:2 | g6:1.5 e6:.5 c6:2 | "
    "a6:4 | r:4 | bb5:.5 bb5:.5 r:.5 bb5:.5 c6:1 d6:1 | c6:.5 c6:.5 r:.5 c6:.5 e6:1 g6:1";
// The counterline under the final A': long horn notes moving against the theme.
const std::string heroCounter =
    "f#4:4 | a4:4 | g4:4 | d4:2 g4:2 | bb4:4 | c5:2 e5:2 | d5:2 a4:2 | f#4:4";

// Zero lives, 180 bpm, 36 bars: low theme over the riff, high theme, bridge, tutti, alarm tag.
const Chord darkM[] = {Dm, Bb, C, Dm, Dm, Bb, Gm, A};
const Chord darkN[] = {Gm, Eb, Bb, F, Gm, Eb, C, A};
const Chord darkTag[] = {Bb, C, A, A};
const std::string darkTheme =
    "d5:.5 d5:.5 a5:1 g5:.5 f5:.5 e5:1 | f5:1.5 d5:.5 bb4:2 | c5:.5 e5:.5 g5:1 c6:1 bb5:.5 g5:.5 | a5:3 r:1 | "
    "d6:.5 c6:.5 a5:1 f5:.5 g5:.5 a5:1 | bb5:1 d6:1 f6:1.5 e6:.5 | d6:1 bb5:.5 g5:.5 d6:1 bb5:1 | c#6:1 e6:1 a6:1 g6:.5 e6:.5";
const std::string darkBridge =
    "g5:2 bb5:1 d6:1 | eb6:2 d6:1 bb5:1 | f5:1 bb5:1 d6:2 | c6:1.5 a5:.5 f5:2 | "
    "g5:1 d6:1 g6:2 | g6:1 f6:.5 eb6:.5 bb5:2 | e6:1 c6:.5 g5:.5 e6:1 g6:1 | a6:1 g6:.5 f6:.5 e6:1 c#6:1";
const std::string darkAlarm =
    "bb5:.5 bb5:.5 r:.5 bb5:.5 c6:.5 d6:.5 f6:1 | g6:.5 g6:.5 r:.5 g6:.5 e6:.5 c6:.5 g5:1 | "
    "a5:.5 c#6:.5 e6:.5 a6:.5 g6:.5 e6:.5 c#6:.5 a5:.5 | a6:2 r:2";

struct Section { const Chord *chords; int size; std::string melody; };
Section section(bool climax, int phrase) {
    if (climax) {
        if (phrase == 4) return {darkTag, 4, darkAlarm};
        if (phrase == 2) return {darkN, 8, darkBridge};
        return {darkM, 8, darkTheme};
    }
    switch (phrase) {
        case 0: return {heroA, 8, heroThemeA};
        case 2: return {heroB, 8, heroThemeB};
        case 4: return {heroTag, 8, heroThemeTag};
        default: return {heroA2, 8, heroThemeA2};
    }
}

void orchestralArrangement(Mix &m, bool climax) {
    Orchestra o(m);
    const int bars = m.beats / 4;
    for (int bar = 0; bar < bars; ++bar) {
        const double beat = bar * 4.0;
        const int phrase = bar / 8, position = bar % 8;
        const Section s = section(climax, phrase);
        const Chord &chord = s.chords[position];
        const bool bridge = phrase == 2, tag = phrase == 4, light = !climax && bridge && position < 4;
        const int root = up(chord.root, 33), fifth = up(chord.fifth, root + 1);
        const bool lastBar = position == s.size - 1 || bar == bars - 1;

        // Low strings drive in eighths; at zero lives they carry the flat-second riff.
        for (int step = 0; step < 8; ++step) {
            const bool accent = step == 0 || step == 3 || step == 6;
            if (light && step % 2) continue;
            int pitch;
            if (climax) { const int riff[8] = {0, 0, 12, 0, 0, 10, 0, 1}; pitch = root + riff[step]; }
            else { const int march[8] = {root, root, root + 12, root, root, root, fifth, root + 12}; pitch = march[step]; }
            bassNote(m, o, beat + step * 0.5, light ? 0.9 : 0.4, pitch, accent ? 0.78 : 0.5, !light);
        }
        if (climax && phrase != 2)
            for (double at : {0.0, 1.5, 3.0}) brass(o.horns, m.g, m.at(beat + at), m.span(0.5), up(chord.root, 41), 0.75, -0.3, true);

        figure(m, o, beat, chord, light ? 0.42 : 0.58, climax || phrase == 1 || phrase == 3, light ? 12 : 0);
        chordPad(m, o, beat, 3.9, chord, light ? 0.6 : 0.45, 55);
        if (bridge || light) hornPad(m, o, beat, 3.9, chord, 0.5);
        if (tag || (!climax && (position == 3 || position == 7) && !bridge) || (climax && phrase == 3 && position % 2 == 0))
            stabs(m, o, beat, chord, 0.72);

        // Percussion: concert bass drum and a march snare with ruffs; full kit at zero lives.
        if (climax) {
            kit(m, o, beat, bridge ? 1 : 3, 0.72, lastBar);
            timpani(o.timpani, m.g, m.exact(beat), up(chord.root, 41), 0.72);
            timpani(o.timpani, m.g, m.exact(beat + 2), up(chord.root, 41), 0.5);
            if (!bridge) { tom(o.kick, m.g, m.exact(beat + 2.5), 45, 0.45, -0.3); tom(o.kick, m.g, m.exact(beat + 3.5), 41, 0.5, 0.3); }
        } else {
            kick(o.kick, m.g, m.exact(beat), light ? 0.55 : 0.8, true);
            if (!light) kick(o.kick, m.g, m.exact(beat + 2), 0.6, true);
            timpani(o.timpani, m.g, m.exact(beat), up(chord.root, 41), position % 4 == 0 ? 0.7 : 0.45);
            if (lastBar && bar + 1 < bars) snareRoll(m, o, beat + 2, beat + 4, 0.125, 0.3, 0.85);
            else if (!light) {
                const double march[7] = {1, 1.75, 2.5, 3, 3.5, 3.75, 0.5}, level[7] = {0.8, 0.35, 0.4, 0.8, 0.35, 0.45, 0.3};
                for (int k = 0; k < 7; k++) snare(o.snare, m.g, m.exact(beat + march[k]), level[k]);
            }
            for (int q = 0; q < (light ? 4 : 8); q++) hat(o.hats, m.g, m.exact(beat + q * (light ? 1 : 0.5)), 0.32, false);
        }
        if (position == 0) cymbal(o.cymbals, m.g, m.exact(beat), climax ? 0.55 : 0.48);
        if (lastBar && bar + 1 < bars) cymbal(o.cymbals, m.g, m.exact(beat + 4), 0.42, m.span(1.5));
    }

    for (int phrase = 0; phrase * 32 < m.beats; ++phrase) {
        const Section s = section(climax, phrase);
        const auto notes = parse(s.melody, phrase * 32);
        if (climax) {
            if (phrase == 0) { line(m, o, Horns, notes, 0.92, -12); line(m, o, Violins, notes, 0.5, -12, -0.1); }
            else if (phrase == 2) { line(m, o, Violins, notes, 0.78, 0); line(m, o, Horns, notes, 0.62, -12); }
            else { line(m, o, Trumpets, notes, 0.85, 0); line(m, o, Violins, notes, 0.72, 0, -0.1); line(m, o, Horns, notes, 0.66, -12); }
            continue;
        }
        if (phrase == 2) {
            line(m, o, Violins, notes, 0.74, 0);
            line(m, o, Flute, notes, 0.6, 12);
        } else {
            line(m, o, Trumpets, notes, 0.86, 0);
            line(m, o, Horns, notes, phrase == 0 ? 0.62 : 0.55, -12);
            if (phrase >= 1) line(m, o, Violins, notes, 0.66, phrase == 4 ? 12 : 0, -0.1);
        }
        if (phrase == 3) line(m, o, Horns, parse(heroCounter, phrase * 32), 0.6, 0);
    }
}

void renderIronwing(const std::filesystem::path &output, bool stems) {
    for (bool climax : {false, true}) {
        const char *base = climax ? "ironwing-climax" : "ironwing-combat";
        Mix m(climax ? 180 : 156, climax ? 144 : 160, 6, true, climax ? 0x4f524343 : 0x4f524351);
        orchestralArrangement(m, climax);
        double gain = 0;
        const auto path = (output / base).string();
        writeWav(path + ".wav", master(m, .75, 2.1, climax ? .161 : .149, gain, stems ? path : ""));
    }
}

// ---------------------------------------------------------------- Yuno oro
// Yuno oro, in homage to the rock-orchestral finales of 2000s duel anime: a supersaw lead
// with echoes, doubled overdriven guitars, choir, rising "shooting star" arpeggios and a
// four-on-the-floor chorus. Original melodies; the tonal centre stays on D.
const Chord Em{4, 7, 11}, Fs{6, 10, 1};

// Synth lead: three detuned saws and a sub-octave sine through a sweeping low-pass.
void synthLead(Bus &b, Rng &g, double t0, double dur, double midi, double vel, double pan) {
    double f = hz(midi), attack = 0.006, release = 0.14; int n = int((dur + release) * SR); long i0 = std::lround(t0 * SR);
    const double cents[3] = {-9, 0, 9}, spread[3] = {-0.35, 0, 0.35};
    double vibPhase = g.uni();
    for (int v = 0; v < 3; v++) {
        double detune = std::pow(2.0, (cents[v] + g.bi() * 2) / 1200.0), phase = g.uni(), a = 0, c = 0, subPhase = g.uni();
        Pan p(pan + spread[v]);
        for (int i = 0; i < n; i++) {
            double t = double(i) / SR;
            double vib = 1 + 0.005 * smooth((t - 0.22) / 0.3) * std::sin(TAU * (5.6 * t + vibPhase));
            double dt = f * detune * vib / SR; phase += dt; if (phase >= 1) phase -= 1;
            double s = 2 * phase - 1;
            if (phase < dt) { double x = phase / dt; s -= x + x - x * x - 1; }
            else if (phase > 1 - dt) { double x = (phase - 1) / dt; s -= x * x + x + x + 1; }
            double cutoff = std::min(12000.0, f * 3 + 1800 + 2600 * std::exp(-t / 0.25)), k = 1 - std::exp(-TAU * cutoff / SR);
            a += k * (s - a); c += k * (a - c);
            subPhase += f * 0.5 * vib / SR;
            double e = smooth(t / attack) * (0.82 + 0.18 * std::exp(-t / 0.1)) * (t > dur ? std::exp(-(t - dur) / (release * 0.3)) : 1) * smooth((n - i) / 200.0);
            b.add(i0 + i, (c + (v == 1 ? 0.35 * std::sin(TAU * subPhase) : 0)) * e * vel * 0.42, p.l, p.r);
        }
    }
}

// Choir "ah": detuned saws through three vowel formants.
void choirVoice(Bus &b, Rng &g, double t0, double dur, double midi, double vel, double pan) {
    double f = hz(midi), attack = 0.18, release = 0.4; int n = int((dur + release) * SR); long i0 = std::lround(t0 * SR);
    const double formant[3] = {760, 1150, 2850}, q[3] = {6, 8, 11}, level[3] = {1, 0.55, 0.22};
    double c0[3], c1[3], c2[3], c3[3], c4[3];
    for (int k = 0; k < 3; k++) {
        double w = TAU * formant[k] / SR, alpha = std::sin(w) / (2 * q[k]), a0 = 1 + alpha;
        c0[k] = alpha / a0; c1[k] = 0; c2[k] = -alpha / a0; c3[k] = -2 * std::cos(w) / a0; c4[k] = (1 - alpha) / a0;
    }
    for (int v = 0; v < 4; v++) {
        double detune = std::pow(2.0, (g.bi() * 12) / 1200.0), phase = g.uni(), rate = 4.8 + g.uni(), vp = g.uni();
        double x1 = 0, x2 = 0, y1[3] = {}, y2[3] = {}; Pan p(pan + (v - 1.5) * 0.25);
        for (int i = 0; i < n; i++) {
            double t = double(i) / SR;
            phase += f * detune * (1 + 0.004 * smooth(t / 0.5) * std::sin(TAU * (rate * t + vp))) / SR; if (phase >= 1) phase -= 1;
            double x = 2 * phase - 1, out = 0;
            for (int k = 0; k < 3; k++) {
                double y = c0[k] * x + c1[k] * x1 + c2[k] * x2 - c3[k] * y1[k] - c4[k] * y2[k];
                y2[k] = y1[k]; y1[k] = y; out += y * level[k];
            }
            x2 = x1; x1 = x;
            double e = smooth(t / attack) * (t > dur ? std::exp(-(t - dur) / (release * 0.3)) : 1) * smooth((n - i) / 300.0);
            b.add(i0 + i, out * e * vel * 1.6, p.l, p.r);
        }
    }
}

// Power chord (root, fifth, octave) strummed on a plucked string; `drive` distorts the bus.
void powerChord(Bus &b, Rng &g, double t0, double dur, int root, double vel, double pan, bool mute) {
    const int interval[3] = {0, 7, 12};
    for (int k = 0; k < 3; k++)
        pluck(b, g, t0 + k * 0.005 + g.uni() * 0.002, root + interval[k], vel * (k == 2 ? 0.7 : 1), pan, mute ? 0.28 : 3.2, mute ? 0.55 : 0.12, mute ? std::min(dur, 0.14) : dur);
}

void drive(Bus &b, double pre, double cutoff) {
    double k = 1 - std::exp(-TAU * cutoff / SR), norm = std::tanh(pre);
    for (auto *ch : {&b.l, &b.r}) {
        double hp = 0, a = 0, c = 0;
        for (auto &x : *ch) {
            hp += 0.011 * (x - hp);
            double y = std::tanh(pre * (x - hp)) / norm;
            a += k * (y - a); c += k * (a - c); x = float(c);
        }
    }
}

// Ping-pong echo for the lead.
void echo(Bus &b, double seconds, double feedback, double mix) {
    int d = int(seconds * SR); size_t n = b.l.size();
    std::vector<float> wl(n), wr(n); double lp = 0, rp = 0;
    for (size_t i = d; i < n; i++) {
        lp += 0.35 * (b.r[i - d] + feedback * wr[i - d] - lp); rp += 0.35 * (b.l[i - d] + feedback * wl[i - d] - rp);
        wl[i] = float(lp); wr[i] = float(rp);
    }
    for (size_t i = 0; i < n; i++) { b.l[i] += float(mix * wl[i]); b.r[i] += float(mix * wr[i]); }
}

struct Band {
    Orchestra o; Bus &lead, &guitar, &choir, &sparkle;
    Band(Mix &m) : o(m), lead(m.bus("lead", 0.27, 0.30)), guitar(m.bus("guitar", 0.062, 0.10)), choir(m.bus("choir", 0.12, 0.55)), sparkle(m.bus("sparkle", 0.45, 0.60)) {}
};

void leadLine(Mix &m, Band &band, const std::vector<Note> &notes, double vel, int transpose) {
    for (auto &note : notes)
        synthLead(band.lead, m.g, m.at(note.beat), m.span(note.len) * 0.95, note.pitch + transpose, vel * (0.92 + m.g.bi() * 0.04), 0);
}

void choirChord(Mix &m, Band &band, double beat, double beats, const Chord &c, double vel) {
    int pcs[3] = {c.root, c.third, c.fifth};
    for (int k = 0; k < 3; k++) choirVoice(band.choir, m.g, m.at(beat), m.span(beats), up(pcs[k], 57), vel, (k - 1) * 0.5);
    choirVoice(band.choir, m.g, m.at(beat), m.span(beats), up(c.root, 45), vel * 0.8, 0);
}

// A falling star: sixteenths rising through the chord over two octaves and back down.
void starArp(Mix &m, Band &band, double bar, const Chord &c, double vel, int beats = 4) {
    int pcs[3] = {c.root, c.third, c.fifth}, tones[7];
    int base = up(c.root, 74);
    for (int k = 0; k < 7; k++) tones[k] = up(pcs[k % 3], k ? tones[k - 1] + 1 : base);
    for (int step = 0; step < beats * 4; step++) {
        int index = step % 12 < 7 ? step % 12 : 12 - step % 12;
        pluck(band.sparkle, m.g, m.exact(bar + step * 0.25), tones[index], vel * (step % 4 ? 0.6 : 1), (index - 3) * 0.22, 1.4, 0.05);
        if (step % 4 == 0) bell(band.sparkle, m.exact(bar + step * 0.25), tones[index] + 12, vel * 0.35, -(index - 3) * 0.2);
    }
}

// "Estela dorada": 168 bpm, 48 bars. Intro, A, A', B (relative minor), bVI–bVII build,
// chorus in D, tag back into the intro.
const Chord oroCombatChords[48] = {D, D, G, A, D, A, Bm, Fsm, G, D, Em, A, D, A, Bm, Fsm, G, A, D, D,
    Bm, G, D, A, Bm, G, Em, Fs, G, A, Bb, C, D, A, Bm, G, D, A, G, A, Bb, C, D, D, G, A, Bb, A};
int oroCombatPart(int bar) { return bar < 4 ? 0 : bar < 12 ? 1 : bar < 20 ? 2 : bar < 28 ? 3 : bar < 32 ? 4 : bar < 44 ? 5 : 6; }
const char *oroHead = "f#5:1.5 a5:.5 d6:1 c#6:.5 d6:.5 | e6:1.5 c#6:.5 a5:2 | d6:1 c#6:.5 b5:.5 f#5:1 b5:1 | a5:1.5 c#6:.5 f#5:2 | ";
const std::string oroIntro = "d6:.5 e6:.5 g6:1 b6:2 | a6:1 c#7:1 e7:2";
const std::string oroA = std::string(oroHead) + "g5:1 b5:.5 d6:.5 g6:1.5 f#6:.5 | f#6:1 e6:.5 d6:.5 a5:2 | g5:1 b5:.5 e6:.5 d6:1 c#6:1 | e6:2 a5:.5 b5:.5 c#6:1";
const std::string oroA2 = std::string(oroHead) + "g5:1 b5:.5 d6:.5 g6:1.5 a6:.5 | a6:1 g6:.5 f#6:.5 e6:1 c#6:1 | d6:4 | r:2 a5:.5 b5:.5 d6:1";
const std::string oroB = "f#6:2 d6:1 b5:1 | d6:1.5 b5:.5 g5:2 | a5:1 d6:1 f#6:1.5 e6:.5 | e6:2 c#6:2 | "
    "d6:1 f#6:1 b6:2 | b6:1 a6:.5 g6:.5 d6:2 | e6:1 g6:1 b6:1 g6:1 | a#6:2 f#6:1 c#6:1";
const std::string oroBuild = "g5:.5 b5:.5 d6:.5 g6:.5 r:1 d6:.5 g6:.5 | a5:.5 c#6:.5 e6:.5 a6:.5 r:1 e6:.5 a6:.5 | "
    "bb5:.5 d6:.5 f6:.5 bb6:.5 r:.5 f6:.5 bb6:1 | c6:.5 e6:.5 g6:.5 c7:.5 e6:.5 g6:.5 c7:1";
const std::string oroChorus = "a6:2 f#6:1 d6:1 | e6:1.5 f#6:.5 e6:1 c#6:1 | d6:1.5 c#6:.5 b5:1 d6:1 | g6:3 f#6:.5 e6:.5 | "
    "f#6:2 a6:1 d7:1 | c#7:1.5 b6:.5 a6:1 e6:1 | g6:1 f#6:.5 g6:.5 b6:1 a6:1 | a6:3 r:1 | "
    "bb6:1.5 a6:.5 f6:2 | g6:1.5 a6:.5 c7:2 | d7:4 | d7:2 a6:1 f#6:1";
const std::string oroTag = "d6:.5 d6:.5 r:.5 d6:.5 e6:1 g6:1 | e6:.5 e6:.5 r:.5 e6:.5 f#6:1 a6:1 | f6:1 d6:.5 bb5:.5 f6:1 bb6:1 | a6:1 e6:1 c#6:1 a5:1";

// "Cuásar dorado": 176 bpm, 40 bars. D minor theme low and high, a rising lift, the
// awakening in D major and a minor tag that falls back to the riff.
const Chord oroClimaxChords[40] = {Dm, Bb, F, C, Dm, Bb, Gm, A, Dm, Bb, F, C, Dm, Bb, Gm, A,
    Bb, C, Dm, F, Gm, C, F, A, D, G, A, D, Bm, G, A, A, Bb, C, D, D, Gm, A, Bb, A};
int oroClimaxPart(int bar) { return bar < 8 ? 0 : bar < 16 ? 1 : bar < 24 ? 2 : bar < 36 ? 3 : 4; }
const std::string quasarTheme = "d5:.5 f5:.5 a5:1 d6:1 c6:.5 a5:.5 | bb5:1.5 a5:.5 f5:2 | c6:.5 a5:.5 f5:1 a5:1 c6:1 | e6:1.5 d6:.5 c6:1 g5:1 | "
    "d6:.5 e6:.5 f6:1 e6:.5 d6:.5 a5:1 | d6:1 bb5:.5 f5:.5 bb5:1 d6:1 | g6:1.5 f6:.5 d6:1 bb5:1 | a5:.5 c#6:.5 e6:.5 g6:.5 a6:2";
const std::string quasarLift = "f6:2 d6:1 bb5:1 | g6:2 e6:1 c6:1 | a6:1.5 g6:.5 f6:1 d6:1 | c6:3 r:1 | "
    "bb5:1 d6:1 g6:2 | g6:1 a6:.5 bb6:.5 c7:2 | a6:1.5 f6:.5 c6:1 f6:1 | e6:1 c#6:1 a6:2";
const std::string quasarAwake = "f#6:1.5 e6:.5 d6:1 a6:1 | b6:2 a6:1 g6:1 | e6:1.5 f#6:.5 g6:1 c#7:1 | d7:3 r:1 | "
    "f#6:1 d6:.5 f#6:.5 b6:2 | b6:1 c#7:.5 d7:.5 g6:2 | a6:1.5 b6:.5 c#7:1 e7:1 | e7:2 c#7:1 a6:1 | "
    "d7:1.5 c7:.5 bb6:2 | e7:1.5 d7:.5 c7:2 | d7:4 | d7:2 a6:1 f#6:1";
const std::string quasarTag = "g6:.5 g6:.5 r:.5 g6:.5 f6:.5 d6:.5 bb5:1 | a5:.5 c#6:.5 e6:.5 a6:.5 g6:.5 e6:.5 c#6:.5 a5:.5 | "
    "bb5:.5 d6:.5 f6:.5 bb6:.5 a6:.5 f6:.5 d6:.5 bb5:.5 | a6:2 r:2";

void yunoOroArrangement(Mix &m, bool climax) {
    Band band(m); Orchestra &o = band.o;
    const int bars = m.beats / 4;
    for (int bar = 0; bar < bars; ++bar) {
        const double beat = bar * 4.0;
        const Chord &chord = climax ? oroClimaxChords[bar] : oroCombatChords[bar];
        const int part = climax ? oroClimaxPart(bar) : oroCombatPart(bar);
        const bool nextChanges = bar + 1 < bars && (climax ? oroClimaxPart(bar + 1) : oroCombatPart(bar + 1)) != part;
        const bool lastBar = nextChanges || bar == bars - 1;
        // Roles per section.
        const bool intro = !climax && part == 0, verse = !climax && (part == 1 || part == 2), calm = !climax && part == 3;
        const bool build = !climax && part == 4, chorus = (!climax && part == 5) || (climax && part == 3);
        const bool tag = (!climax && part == 6) || (climax && part == 4), lift = climax && part == 2, riff = climax && part < 2;
        const int root = up(chord.root, 33), gtr = up(chord.root, 40);

        // Bass: rock eighths, octave pops in the chorus, quarters in the intro.
        for (int step = 0; step < 8; ++step) {
            if (intro && bar < 2 && step % 2) continue;
            int pitch = root + ((chorus && step % 2) || (riff && (step == 2 || step == 6)) ? 12 : 0);
            bool accent = step == 0 || step == 3 || step == 6;
            bassNote(m, o, beat + step * 0.5, intro && bar < 2 ? 0.9 : 0.42, pitch, accent ? 0.78 : 0.52, !(intro && bar < 2));
        }

        // Doubled guitars, hard left and right.
        for (double side : {-0.85, 0.85}) {
            if (intro && bar < 2) continue;
            if (calm || lift || (intro && bar >= 2)) {
                powerChord(band.guitar, m.g, m.at(beat), m.span(2.4), gtr, 0.8, side, false);
                powerChord(band.guitar, m.g, m.at(beat + 2.5), m.span(1.4), gtr, 0.7, side, false);
                continue;
            }
            for (int step = 0; step < 8; ++step) {
                bool open = step == 0 || step == 3 || step == 6;
                int pitch = gtr;
                if (riff && step >= 6) pitch = gtr + (step == 6 ? 3 : 5);
                if (tag) open = step == 0 || step == 4;
                powerChord(band.guitar, m.g, m.at(beat + step * 0.5), m.span(open ? (step == 6 ? 1.0 : 1.5) : 0.5), pitch, open ? 0.85 : 0.6, side, !open);
            }
        }

        // Orchestra.
        if (verse || chorus || riff) figure(m, o, beat, chord, chorus ? 0.5 : 0.44, chorus || climax, 0);
        if (!(intro && bar < 2)) chordPad(m, o, beat, 3.9, chord, calm || lift ? 0.6 : 0.42, 55);
        if (calm || lift || chorus || build) choirChord(m, band, beat, 3.9, chord, chorus ? 0.55 : 0.5);
        if (calm || lift) hornPad(m, o, beat, 3.9, chord, 0.5);
        if (build || (tag && !climax) || (climax && part == 1 && bar % 2)) stabs(m, o, beat, chord, 0.7);
        if (intro || build || (chorus && bar % 2 == 0) || (lift && bar % 2 == 0)) starArp(m, band, beat, chord, intro ? 0.55 : 0.42);

        // Drums.
        if (intro) {
            if (bar < 2) { kick(o.kick, m.g, m.exact(beat), 0.7, true); for (int q = 0; q < 16; q++) hat(o.hats, m.g, m.exact(beat + q * 0.25), q % 4 ? 0.18 : 0.3, false); }
            else kit(m, o, beat, 3, 0.7, bar == 3);
        } else if (calm || lift) {
            kit(m, o, beat, lift ? 0 : 1, 0.68, lastBar);
        } else if (build) {
            kick(o.kick, m.g, m.exact(beat), 0.8, false); kick(o.kick, m.g, m.exact(beat + 2), 0.75, false);
            snareRoll(m, o, beat, beat + 4, bar % 4 < 2 ? 0.5 : 0.25, 0.35 + 0.1 * (bar % 4), 0.55 + 0.12 * (bar % 4));
            timpani(o.timpani, m.g, m.exact(beat), up(chord.root, 41), 0.75);
        } else if (chorus) {
            kit(m, o, beat, 2, 0.74, lastBar);
            timpani(o.timpani, m.g, m.exact(beat), up(chord.root, 41), 0.65);
            if (bar % 2 == 0) cymbal(o.cymbals, m.g, m.exact(beat), 0.42);
        } else {
            kit(m, o, beat, verse && part == 1 ? 0 : 3, 0.72, lastBar);
            if (climax) timpani(o.timpani, m.g, m.exact(beat), up(chord.root, 41), 0.6);
        }
        if (nextChanges || bar == bars - 1) cymbal(o.cymbals, m.g, m.exact(beat + 4), 0.45, m.span(climax ? 2 : 3));
        else if (bar % 8 == 0 || (climax && bar % 4 == 0)) cymbal(o.cymbals, m.g, m.exact(beat), 0.45);
    }

    // Melodies.
    auto at = [&](int bar) { return bar * 4.0; };
    if (!climax) {
        leadLine(m, band, parse(oroIntro, at(2)), 0.8, -12);
        auto a = parse(oroA, at(4)), a2 = parse(oroA2, at(12)), b = parse(oroB, at(20)), build = parse(oroBuild, at(28)), chorus = parse(oroChorus, at(32)), tag = parse(oroTag, at(44));
        leadLine(m, band, a, 0.85, 0); line(m, o, Violins, a, 0.55, -12);
        leadLine(m, band, a2, 0.88, 0); line(m, o, Violins, a2, 0.6, -12); line(m, o, Horns, a2, 0.5, -12);
        line(m, o, Violins, b, 0.78, -12); line(m, o, Flute, b, 0.55, 0); line(m, o, Horns, b, 0.45, -24);
        leadLine(m, band, build, 0.8, -12); line(m, o, Trumpets, build, 0.75, -12);
        leadLine(m, band, chorus, 0.9, -12); line(m, o, Violins, chorus, 0.62, 0); line(m, o, Trumpets, chorus, 0.82, -12); line(m, o, Horns, chorus, 0.62, -24);
        leadLine(m, band, tag, 0.85, 0); line(m, o, Trumpets, tag, 0.78, -12);
    } else {
        auto low = parse(quasarTheme, at(0)), high = parse(quasarTheme, at(8)), lift = parse(quasarLift, at(16)), awake = parse(quasarAwake, at(24)), tag = parse(quasarTag, at(36));
        line(m, o, Horns, low, 0.9, -12); line(m, o, Violins, low, 0.5, -12, -0.1);
        leadLine(m, band, high, 0.88, 0); line(m, o, Trumpets, high, 0.78, 0); line(m, o, Violins, high, 0.6, 0);
        line(m, o, Violins, lift, 0.8, -12); line(m, o, Flute, lift, 0.55, 0); line(m, o, Horns, lift, 0.55, -24);
        leadLine(m, band, awake, 0.92, -12); line(m, o, Violins, awake, 0.62, 0); line(m, o, Trumpets, awake, 0.85, -12); line(m, o, Horns, awake, 0.65, -24);
        leadLine(m, band, tag, 0.85, -12); line(m, o, Trumpets, tag, 0.8, -12); line(m, o, Violins, tag, 0.6, 0);
    }

    drive(band.guitar, 5.0, 4200);
    echo(band.lead, m.span(0.75), 0.32, 0.28);
}

void renderYunoOro(const std::filesystem::path &output, bool stems) {
    std::thread jobs[2];
    for (int climax = 0; climax < 2; ++climax)
        jobs[climax] = std::thread([&, climax] {
            const char *base = climax ? "yuno-oro-climax" : "yuno-oro-combat";
            Mix m(climax ? 176 : 168, climax ? 160 : 192, 6, true, climax ? 0x51554153 : 0x4f524f31);
            yunoOroArrangement(m, climax);
            double gain = 0;
            const auto path = (output / base).string();
            writeWav(path + ".wav", master(m, .72, 2.2, climax ? .165 : .155, gain, stems ? path : ""));
        });
    for (auto &job : jobs) job.join();
}

// ---------------------------------------------------------------- WD
// WD, in homage to 8-bit light-gun hunting games: two pulse channels, a stepped triangle
// bass, LFSR noise drums and gunshots. Combat is a jaunty D major march; zero lives is a
// D minor chase with a chromatic climb. Original melodies.
const Chord Ab{8, 0, 3};

double polySaw(double ph, double dt) {
    double s = 2 * ph - 1;
    if (ph < dt) { double x = ph / dt; s -= x + x - x * x - 1; }
    else if (ph > 1 - dt) { double x = (ph - 1) / dt; s -= x * x + x + x + 1; }
    return s;
}
// The console's 16 volume steps.
double steps16(double e) { return std::floor(std::clamp(e, 0.0, 1.0) * 15 + 0.5) / 15; }

// Pulse channel: band-limited pulse of the given duty, stepped envelope, optional vibrato and slide.
void pulse(Bus &b, double t0, double dur, double midi, double vel, double pan, double duty, double decay = 0, double sustain = 1, bool vibrato = false, double slideFrom = 0) {
    int n = int((dur + 0.03) * SR); long i0 = std::lround(t0 * SR); double phase = 0; Pan p(pan);
    for (int i = 0; i < n; i++) {
        double t = double(i) / SR, m = midi;
        if (slideFrom) m = slideFrom + (midi - slideFrom) * smooth(t / std::max(dur, 1e-3));
        double f = hz(m) * (vibrato ? 1 + 0.006 * smooth((t - 0.2) / 0.15) * std::sin(TAU * 6 * t) : 1), dt = f / SR;
        phase += dt; if (phase >= 1) phase -= 1;
        double other = phase - duty; if (other < 0) other += 1;
        double s = 0.5 * (polySaw(phase, dt) - polySaw(other, dt));
        double e = decay > 0 ? std::max(sustain, std::exp(-t / decay)) : 1;
        e = steps16(e) * smooth(i / 60.0) * (t > dur ? std::max(0.0, 1 - (t - dur) / 0.03) : 1);
        b.add(i0 + i, s * e * vel, p.l, p.r);
    }
}

// Triangle channel: 32-step triangle without volume control.
void triangle(Bus &b, double t0, double dur, double midi, double vel, double slideTo = 0, double slideTime = 0) {
    int n = int((dur + 0.01) * SR); long i0 = std::lround(t0 * SR); double phase = 0, lp = 0;
    for (int i = 0; i < n; i++) {
        double t = double(i) / SR, m = slideTime > 0 ? midi + (slideTo - midi) * std::min(1.0, t / slideTime) : midi;
        phase += hz(m) / SR; if (phase >= 1) phase -= 1;
        int idx = int(phase * 32); double v = (idx < 16 ? idx : 31 - idx) / 7.5 - 1;
        lp += 0.5 * (v - lp);
        double e = smooth(i / 100.0) * smooth((n - i) / 100.0);
        b.add(i0 + i, lp * e * vel, 0.7071, 0.7071);
    }
}

// Noise channel: 15-bit LFSR clocked at `rate`; short mode gives the metallic loop.
void noise(Bus &b, double t0, double length, double rate, bool shortMode, double vel, double decay, double pan = 0) {
    int n = int(length * SR); long i0 = std::lround(t0 * SR); uint16_t reg = 1; double clock = 0; Pan p(pan);
    for (int i = 0; i < n; i++) {
        clock += rate / SR;
        while (clock >= 1) { clock -= 1; int bit = (reg ^ (reg >> (shortMode ? 6 : 1))) & 1; reg = uint16_t((reg >> 1) | (bit << 14)); }
        double t = double(i) / SR, e = steps16(std::exp(-t / decay)) * smooth(i / 40.0) * smooth((n - i) / 100.0);
        b.add(i0 + i, ((reg & 1) ? 1.0 : -1.0) * e * vel, p.l, p.r);
    }
}

// The console's output filter, which also keeps the encoder from overshooting square edges.
void outputFilter(Bus &b, double cutoff) {
    double k = 1 - std::exp(-TAU * cutoff / SR);
    for (auto *ch : {&b.l, &b.r}) { double a = 0, c = 0; for (auto &x : *ch) { a += k * (x - a); c += k * (a - c); x = float(c); } }
}

struct Chip {
    Bus &lead, &second, &tri, &drums, &fx;
    Chip(Mix &m) : lead(m.bus("pulse1", 0.25, 0.35)), second(m.bus("pulse2", 0.22, 0.35)), tri(m.bus("triangle", 0.095, 0.05)),
        drums(m.bus("noise", 0.19, 0.10)), fx(m.bus("shots", 0.26, 0.35)) {}
};

void chipKick(Mix &m, Chip &c, double beat, double vel) { triangle(c.tri, m.exact(beat), 0.11, 62, vel, 30, 0.08); }
void chipSnare(Mix &m, Chip &c, double beat, double vel) { noise(c.drums, m.exact(beat), 0.16, 12000, false, vel, 0.06, 0.05); }
void chipHat(Mix &m, Chip &c, double beat, double vel, bool open = false) { noise(c.drums, m.exact(beat), open ? 0.25 : 0.05, 44100, false, vel * 0.55, open ? 0.08 : 0.014, 0.25); }
void chipCrash(Mix &m, Chip &c, double beat, double vel) { noise(c.drums, m.exact(beat), 1.2, 30000, false, vel * 0.6, 0.35, -0.2); }
// Light-gun shot: a noise blast and a falling pulse.
void shot(Mix &m, Chip &c, double beat, double vel) {
    noise(c.fx, m.exact(beat), 0.35, 9000, false, vel, 0.09, 0.15);
    pulse(c.fx, m.exact(beat), 0.14, 40, vel * 0.5, -0.1, 0.5, 0.05, 0, false, 88);
}

void chipLine(Mix &m, Chip &c, const std::vector<Note> &notes, double vel, int transpose, double duty, bool echo) {
    for (auto &note : notes) {
        double d = m.span(note.len) * (note.len <= 0.25 ? 0.8 : 0.9);
        pulse(c.lead, m.exact(note.beat), d, note.pitch + transpose, vel, -0.12, duty, 0.5, 0.62, note.len >= 1);
        if (echo) pulse(c.second, m.exact(note.beat + 0.5), d, note.pitch + transpose, vel * 0.42, 0.35, 0.125, 0.4, 0.5, note.len >= 1);
    }
}

// Sixteenth arpeggio through the chord over two octaves: the flutter of wings.
void flutter(Mix &m, Chip &c, double bar, const Chord &ch, double vel, int base = 69) {
    int pcs[3] = {ch.root, ch.third, ch.fifth}, tones[6];
    for (int k = 0; k < 6; k++) tones[k] = up(pcs[k % 3], k ? tones[k - 1] + 1 : base);
    for (int step = 0; step < 16; step++) {
        int index = step % 10 < 6 ? step % 10 : 10 - step % 10;
        pulse(c.second, m.exact(bar + step * 0.25), m.span(0.2), tones[index], vel * (step % 4 ? 0.7 : 1), 0.3, 0.25, 0.08, 0.3);
    }
}

// "Temporada de caza": 152 bpm, 40 bars. Ready jingle, A, A', B, a flying section, tag.
const Chord wdCombatChords[40] = {D, D, G, A, D, G, D, A, D, G, A, D, D, G, D, A, D, G, A, D,
    Bm, G, Em, A, Fsm, Bm, G, A, Bb, C, D, D, Bb, C, A, A, G, A, D, A};
int wdCombatPart(int bar) { return bar < 4 ? 0 : bar < 12 ? 1 : bar < 20 ? 2 : bar < 28 ? 3 : bar < 36 ? 4 : 5; }
const std::string wdIntro = "d5:.5 f#5:.5 a5:.5 d6:1 r:1.5 | r:4 | g5:.5 b5:.5 d6:.5 g6:1 r:1.5 | a5:.5 c#6:.5 e6:.5 a6:1 r:1.5";
const char *wdHead = "a5:.5 f#5:.5 a5:.5 d6:1 a5:.5 f#5:1 | g5:.5 b5:.5 d6:.5 g6:1 d6:.5 b5:1 | a5:.75 a5:.25 f#5:.5 a5:.5 d6:.5 e6:.5 f#6:1 | e6:1 c#6:.5 a5:.5 e5:1 r:1 | ";
const std::string wdA = std::string(wdHead) + "a5:.5 f#5:.5 a5:.5 d6:1 a5:.5 f#5:1 | g5:.5 b5:.5 d6:.5 g6:.5 b6:1 a6:.5 g6:.5 | f#6:.5 e6:.5 c#6:.5 e6:.5 a6:1 g6:.5 e6:.5 | d6:1 a5:.5 f#5:.5 d5:1 r:1";
const std::string wdA2 = std::string(wdHead) + "d6:.5 a5:.5 d6:.5 f#6:1 d6:.5 a5:1 | b5:.5 d6:.5 g6:.5 b6:.5 d7:1 b6:1 | a6:.75 g6:.25 f#6:.5 e6:.5 c#6:.5 e6:.5 a5:1 | d6:2 r:1 a5:.5 c#6:.5";
const std::string wdB = "d6:1.5 c#6:.5 b5:1 f#5:1 | g5:1.5 a5:.5 b5:1 d6:1 | e6:1.5 d6:.5 b5:1 g5:1 | a5:3 r:1 | "
    "c#6:1.5 b5:.5 a5:1 c#6:1 | d6:1.5 e6:.5 f#6:2 | g6:1 f#6:.5 e6:.5 d6:1 b5:1 | c#6:1 e6:1 a6:1 r:1";
const std::string wdFly = "d6:2 f6:2 | e6:2 g6:2 | f#6:4 | a6:2 f#6:1 d6:1 | bb6:2 f6:2 | c7:2 g6:2 | a6:2 e6:1 c#6:1 | e6:2 a6:2";
const std::string wdTag = "d6:.5 r:.5 d6:.5 r:.5 b5:.5 d6:.5 g6:1 | e6:.5 r:.5 e6:.5 r:.5 c#6:.5 e6:.5 a6:1 | f#6:.5 a6:.5 d7:1 a6:.5 f#6:.5 d6:1 | e6:1 r:1 a5:.5 c#6:.5 e6:1";

// "Última bala": 184 bpm, 40 bars. Theme, theme with echo, a Phrygian turn, a chromatic
// chase and a tag of shots.
const Chord wdClimaxChords[40] = {Dm, Bb, Gm, A, Dm, Bb, C, A, Dm, Bb, Gm, A, Dm, Bb, C, A,
    Gm, Dm, Eb, A, Gm, Dm, Eb, A, Dm, Eb, E, F, Fs, G, Ab, A, Bb, C, Dm, Dm, Bb, C, A, A};
int wdClimaxPart(int bar) { return bar < 8 ? 0 : bar < 16 ? 1 : bar < 24 ? 2 : bar < 32 ? 3 : 4; }
const std::string wdLast = "d6:.5 d6:.25 d6:.25 f6:.5 a6:.5 g6:.5 f6:.5 e6:.5 f6:.5 | d6:1 bb5:.5 d6:.5 f6:1.5 r:.5 | "
    "g6:.5 g6:.25 g6:.25 bb6:.5 g6:.5 d6:.5 bb5:.5 g5:1 | a5:.5 c#6:.5 e6:.5 a6:.5 g6:.5 e6:.5 c#6:1 | "
    "d6:.5 f6:.5 a6:1 a6:.5 g6:.5 f6:1 | f6:.5 d6:.5 bb5:1 bb5:.5 c6:.5 d6:1 | e6:.5 g6:.5 c7:1 bb6:.5 g6:.5 e6:1 | c#6:.5 e6:.5 a6:.5 e6:.5 c#6:.5 a5:.5 r:1";
const std::string wdTurn = "bb5:2 d6:1 g6:1 | f6:2 e6:1 d6:1 | eb6:1.5 g6:.5 bb6:2 | a6:2 e6:1 c#6:1 | "
    "d6:1 g6:1 bb6:2 | a6:1 f6:1 d6:2 | g6:1 bb6:1 eb7:2 | c#7:2 a6:2";
const std::string wdLastTag = "f6:.5 r:.5 f6:.5 r:.5 d6:.5 f6:.5 bb6:1 | g6:.5 r:.5 g6:.5 r:.5 e6:.5 g6:.5 c7:1 | a6:1.5 g6:.5 f6:1 d6:1 | a5:.5 d6:.5 f6:.5 a6:.5 d7:2 | "
    "d7:.5 r:.5 d7:.5 r:.5 bb6:.5 f6:.5 d6:1 | e7:.5 r:.5 e7:.5 r:.5 c7:.5 g6:.5 e6:1 | c#7:1 a6:1 e6:1 c#6:1 | a5:1 r:3";

void wdArrangement(Mix &m, bool climax) {
    Chip c(m);
    const int bars = m.beats / 4;
    for (int bar = 0; bar < bars; ++bar) {
        const double beat = bar * 4.0;
        const Chord &ch = climax ? wdClimaxChords[bar] : wdCombatChords[bar];
        const int part = climax ? wdClimaxPart(bar) : wdCombatPart(bar);
        const bool lastBar = bar == bars - 1 || (climax ? wdClimaxPart(bar + 1) : wdCombatPart(bar + 1)) != part;
        const int root = up(ch.root, 38), fifth = up(ch.fifth, root - 12);
        const bool intro = !climax && part == 0, tag = (!climax && part == 5) || (climax && part == 4);
        const bool fly = !climax && part == 4, chase = climax && part == 3, calm = !climax && part == 3;

        // Triangle bass.
        if (climax) {
            for (int step = 0; step < 8; step++) {
                if (part == 2 && step % 4 == 1) continue;
                triangle(c.tri, m.exact(beat + step * 0.5), m.span(0.42), root + (step % 2 ? 12 : 0), 0.9);
            }
        } else if (intro && bar < 2) {
            triangle(c.tri, m.exact(beat), m.span(bar == 0 ? 3.9 : 0.9), root, 0.9);
            if (bar == 0) chipHat(m, c, beat + 3, 0.8, true);
        } else if (calm) {
            triangle(c.tri, m.exact(beat), m.span(1.9), root, 0.9);
            triangle(c.tri, m.exact(beat + 2), m.span(1.4), fifth + 12, 0.9);
            triangle(c.tri, m.exact(beat + 3.5), m.span(0.45), root + 12, 0.9);
        } else if (fly) {
            for (int step = 0; step < 8; step++) triangle(c.tri, m.exact(beat + step * 0.5), m.span(0.42), root + (step % 2 ? 12 : 0), 0.9);
        } else {
            for (int q = 0; q < 4; q++) triangle(c.tri, m.exact(beat + q), m.span(0.7), q % 2 ? fifth : root, 0.9);
            triangle(c.tri, m.exact(beat + 3.5), m.span(0.4), root + 12, 0.9);
        }

        // Second pulse: offbeat chord stabs, or the flutter of wings.
        const bool stabs = (!climax && (part == 1 || tag || (intro && bar >= 2))) || (climax && (part == 2 || tag));
        if (stabs)
            for (int q = 0; q < 4; q++) {
                pulse(c.second, m.exact(beat + q + 0.5), m.span(0.3), up(ch.third, 66), 0.75, 0.3, 0.125, 0.1, 0.3);
                pulse(c.second, m.exact(beat + q + 0.75), m.span(0.2), up(ch.fifth, 66), 0.55, 0.3, 0.125, 0.08, 0.25);
            }
        if (fly || chase || (climax && part == 0)) flutter(m, c, beat, ch, chase ? 0.85 : 0.75, climax ? 62 : 69);
        if (intro && bar == 1)
            for (int k = 0; k < 16; k++) pulse(c.second, m.exact(beat + k * 0.125), m.span(0.11), k % 2 ? 83 : 81, 0.6, 0.3, 0.25, 0.05, 0.4);

        // Chase: the lead trembles between root and fifth over the chromatic climb.
        if (chase)
            for (int k = 0; k < 16; k++)
                pulse(c.lead, m.exact(beat + k * 0.25), m.span(0.2), (k % 2 ? up(ch.fifth, 74) : up(ch.root, 74)), 0.8, -0.12, 0.25, 0.1, 0.5);

        // Noise drums.
        if (intro && bar < 2) {
            if (bar == 1) { shot(m, c, beat + 2, 0.9); shot(m, c, beat + 3, 1.0); }
        } else {
            const bool driving = climax || fly;
            if (climax && part != 2) for (double k : {0.0, 1.5, 2.0, 3.0}) chipKick(m, c, beat + k, 1.0);
            else if (fly) for (int q = 0; q < 4; q++) chipKick(m, c, beat + q, 1.0);
            else { chipKick(m, c, beat, 1.0); chipKick(m, c, beat + 2, 0.9); }
            if (lastBar) {
                chipSnare(m, c, beat + 1, 0.9);
                for (int k = 0; k < 8; k++) chipSnare(m, c, beat + 2 + k * 0.25, 0.5 + 0.06 * k);
            } else if (calm) {
                chipSnare(m, c, beat + 2, 0.8);
            } else {
                chipSnare(m, c, beat + 1, 0.9); chipSnare(m, c, beat + 3, 0.9);
            }
            int hats = driving ? 16 : 8;
            for (int k = 0; k < hats; k++) chipHat(m, c, beat + k * 4.0 / hats, k * 4 % hats == 0 ? 1.0 : 0.6, !driving && k == 7 && !lastBar);
        }
        if ((bar % 8 == 0 && bar > 0 && !intro) || (!climax && bar == 4)) chipCrash(m, c, beat, 0.9);
        if (tag && (bar % 4 == 0 || bar % 4 == 1)) { shot(m, c, beat + 0.5, 0.8); shot(m, c, beat + 1.5, 0.85); }
        if (bar == bars - 1 && climax) for (double k : {1.0, 2.0, 3.0}) shot(m, c, beat + k, 0.5 + 0.07 * k);
        if (bar == bars - 1 && !climax) shot(m, c, beat + 1, 0.9);
    }

    if (!climax) {
        chipLine(m, c, parse(wdIntro, 0), 0.85, 0, 0.5, false);
        chipLine(m, c, parse(wdA, 16), 0.85, 0, 0.25, false);
        chipLine(m, c, parse(wdA2, 48), 0.85, 0, 0.25, true);
        chipLine(m, c, parse(wdB, 80), 0.82, 0, 0.5, true);
        chipLine(m, c, parse(wdFly, 112), 0.85, 0, 0.125, false);
        chipLine(m, c, parse(wdTag, 144), 0.88, 0, 0.25, false);
    } else {
        chipLine(m, c, parse(wdLast, 0), 0.85, -12, 0.25, false);
        chipLine(m, c, parse(wdLast, 32), 0.85, 0, 0.125, true);
        chipLine(m, c, parse(wdTurn, 64), 0.85, -12, 0.5, false);
        chipLine(m, c, parse(wdLastTag, 128), 0.88, -12, 0.25, false);
    }
    for (Bus *bus : {&c.lead, &c.second, &c.tri, &c.drums, &c.fx}) outputFilter(*bus, 9000);
}

void renderWd(const std::filesystem::path &output, bool stems) {
    for (bool climax : {false, true}) {
        const char *base = climax ? "wd-climax" : "wd-combat";
        Mix m(climax ? 184 : 152, 160, 4, true, climax ? 0x57444c42 : 0x57444348);
        wdArrangement(m, climax);
        double gain = 0;
        const auto path = (output / base).string();
        writeWav(path + ".wav", master(m, .6, 1.3, .15, gain, stems ? path : ""));
    }
}

int main(int argc, char **argv) {
    std::filesystem::path output = argc > 1 ? argv[1] : "/tmp/relick-audio-wav";
    bool stems = false; std::string only;
    for (int i = 2; i < argc; i++) { std::string arg = argv[i]; if (arg == "--stems") stems = true; else if (arg.rfind("--only=", 0) == 0) only = arg.substr(7); }
    std::filesystem::create_directories(output);
    if (only == "yuno-oro") { renderYunoOro(output, stems); return 0; }
    if (only == "wd") { renderWd(output, stems); return 0; }
    auto name = [&](const char *file) { return (output / file).string(); };
    auto stem = [&](const char *file) { return stems ? name(file) : std::string(); };
    std::thread calm([&] {
        Mix m(126, 144, 6, true, 0x4d454e55); menu(m); double gain = 0;
        writeWav(name("menu.wav"), master(m, 0.9, 2.6, 0.125, gain, stem("menu")));
    });
    std::thread battle([&] {
        Mix m(160, 192, 6, true, 0x59554e4f); Orchestra o(m); combat(m, o); double gain = 0;
        writeWav(name("yuno-combat.wav"), master(m, 0.75, 2.2, 0.155, gain, stem("combat")));
        Mix lead(160, 8, 3, false, 0x4c454144); Orchestra lo(lead); toCombat(lead, lo);
        writeWav(name("yuno-to-combat.wav"), master(lead, 0.75, 2.2, 0, gain, stem("to-combat")));
    });
    std::thread crisis([&] {
        Mix m(168, 160, 6, true, 0x5a45524f); Orchestra o(m); climax(m, o); double gain = 0;
        writeWav(name("yuno-climax.wav"), master(m, 0.75, 2.2, 0.165, gain, stem("climax")));
        Mix lead(168, 8, 3, false, 0x4c415354); Orchestra lo(lead); toClimax(lead, lo);
        writeWav(name("yuno-to-climax.wav"), master(lead, 0.75, 2.2, 0, gain, stem("to-climax")));
    });
    calm.join(); battle.join(); crisis.join();
    {
        Mix m(144, 160, 6, true, 0x4f53414d); osamu(m, false); double gain = 0;
        writeWav(name("osamu-combat.wav"), master(m, 0.73, 2.1, 0.151, gain, stem("osamu-combat")));
    }
    {
        Mix m(174, 128, 6, true, 0x3030564c); osamu(m, true); double gain = 0;
        writeWav(name("osamu-climax.wav"), master(m, 0.73, 2.1, 0.165, gain, stem("osamu-climax")));
    }
    renderIronwing(output, stems);
    renderYunoOro(output, stems);
    renderWd(output, stems);
    std::cout << "Relick score written to " << output.string() << "\n";
    return 0;
}
