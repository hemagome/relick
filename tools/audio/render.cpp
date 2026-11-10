// Original Relick score and sound design. No samples, external music or libraries.
// Build: g++ -O2 -std=c++17 tools/audio/render.cpp -o /tmp/relick-audio
// Run: /tmp/relick-audio /tmp/relick-audio-wav
#include <algorithm>
#include <array>
#include <cmath>
#include <cstdint>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <random>
#include <string>
#include <vector>

constexpr int SR = 44100;
constexpr double PI = 3.14159265358979323846;
constexpr double BEAT = 60.0 / 160.0;
constexpr int BARS = 32;
struct Frame { double l = 0, r = 0; };
using Audio = std::vector<Frame>;
std::mt19937 rng(0x52454c49);
double noise() { return double(rng()) / rng.max() * 2 - 1; }
double hz(int midi) { return 440 * std::pow(2.0, (midi - 69) / 12.0); }
double sine(double phase) { return std::sin(2 * PI * phase); }

void add(Audio &a, int index, double value, double pan, bool wrap = true) {
    if (wrap) index = (index % int(a.size()) + int(a.size())) % int(a.size());
    if (index < 0 || index >= int(a.size())) return;
    double angle = (pan + 1) * PI / 4;
    a[index].l += value * std::cos(angle);
    a[index].r += value * std::sin(angle);
}

// Band-limited harmonic voices: rounded electric piano, bowed pad, marimba and lead.
enum Voice { Piano, Pad, Pluck, Lead, Bass, Bell };
void note(Audio &a, double beat, double beats, int pitch, Voice voice, double gain, double pan, bool wrap = true) {
    double f = hz(pitch), duration = beats * BEAT;
    double release = voice == Pad ? 0.4 : voice == Piano ? 0.32 : voice == Bell ? 0.65 : 0.09;
    int begin = std::lround(beat * BEAT * SR), count = int((duration + release) * SR);
    for (int i = 0; i < count; i++) {
        double t = double(i) / SR, p = f * t, signal = 0;
        double attack = voice == Pad ? 0.10 : voice == Lead ? 0.014 : 0.003;
        double envelope = std::min(1.0, t / attack);
        if (t > duration) envelope *= std::pow(std::max(0.0, 1 - (t - duration) / release), 2);
        if (voice == Piano) {
            signal = sine(p) + 0.30 * sine(p * 2) * std::exp(-t * 5) +
                0.14 * sine(p * 3.002) * std::exp(-t * 8) + 0.05 * sine(p * 6.01) * std::exp(-t * 15);
            envelope *= std::exp(-t * 1.9);
        } else if (voice == Pad) {
            for (int harmonic = 1; harmonic <= 6 && harmonic * f < 11000; harmonic++) {
                double weight = 0.5 / std::pow(harmonic, 1.7);
                signal += weight * (sine(p * harmonic * 0.998) + sine(p * harmonic * 1.002));
            }
            envelope *= 0.84 + 0.16 * sine(t * 0.6);
        } else if (voice == Pluck) {
            signal = sine(p) + 0.30 * sine(p * 2) + 0.14 * sine(p * 3) + 0.06 * sine(p * 4);
            envelope *= std::exp(-t * 10);
        } else if (voice == Lead) {
            double vibrato = 0.0024 * sine(t * 5) * std::min(1.0, t * 4);
            for (int harmonic = 1; harmonic <= 8 && harmonic * f < 11000; harmonic++)
                signal += sine(p * harmonic + vibrato * harmonic) / std::pow(harmonic, 1.65);
            envelope *= 0.88 + 0.12 * std::exp(-t * 5);
        } else if (voice == Bass) {
            signal = sine(p) + 0.25 * sine(p * 2) + 0.12 * sine(p * 3);
            envelope *= std::exp(-t * 2.4);
        } else {
            signal = sine(p) * std::exp(-t * 2.8) + 0.35 * sine(p * 2.756) * std::exp(-t * 5) +
                0.16 * sine(p * 5.405) * std::exp(-t * 9);
        }
        add(a, begin + i, signal * envelope * gain, pan, wrap);
    }
}

enum Drum { Kick, Snare, Hat, OpenHat, Tom, Crash };
void drum(Audio &a, double beat, Drum kind, double gain, double pan = 0) {
    double length = kind == Crash ? 1.2 : kind == OpenHat ? 0.3 : kind == Hat ? 0.065 : 0.23;
    int begin = std::lround(beat * BEAT * SR);
    double lastNoise = 0, filtered = 0;
    for (int i = 0; i < int(length * SR); i++) {
        double t = double(i) / SR, n = noise(), high = n - lastNoise; lastNoise = n;
        filtered += 0.13 * (n - filtered);
        double value;
        if (kind == Kick)
            value = sine(47 * t + 65 * 0.025 * (1 - std::exp(-t / 0.025))) * std::exp(-t * 21) +
                high * 0.08 * std::exp(-t * 150);
        else if (kind == Snare)
            value = (0.50 * high + 0.32 * filtered) * std::exp(-t * 22) + sine(178 * t) * 0.34 * std::exp(-t * 30);
        else if (kind == Tom)
            value = sine(100 * t + 1.1 * (1 - std::exp(-t * 30))) * std::exp(-t * 18) + high * 0.08 * std::exp(-t * 35);
        else
            value = high * std::exp(-t * (kind == Hat ? 65 : kind == OpenHat ? 17 : 4)) * 0.45;
        add(a, begin + i, value * gain * std::min(1.0, t * 3000), pan);
    }
}

void space(Audio &audio, bool loop, double wet) {
    Audio dry = audio;
    // Stereo reflections are wrapped for a periodic tail across the loop boundary.
    for (auto [seconds, gain] : std::array<std::pair<double, double>, 5>{{
            {0.071, 0.26}, {0.113, 0.20}, {0.181, 0.15}, {BEAT * 0.75, 0.13}, {BEAT * 1.5, 0.08}}}) {
        int delay = int(seconds * SR);
        for (int i = 0; i < int(audio.size()); i++) {
            int source = i - delay;
            if (source < 0) { if (!loop) continue; source += int(audio.size()); }
            audio[i].l += dry[source].r * gain * wet;
            audio[i].r += dry[source].l * gain * wet;
        }
    }
}

void writeWav(const std::string &path, Audio audio, bool loop, double targetRms) {
    // Remove DC, then retain dynamics with common RMS targets and a soft peak ceiling.
    double dcL = 0, dcR = 0, energy = 0;
    for (auto s : audio) { dcL += s.l; dcR += s.r; }
    dcL /= audio.size(); dcR /= audio.size();
    for (auto &s : audio) { s.l -= dcL; s.r -= dcR; energy += s.l*s.l + s.r*s.r; }
    double gain = targetRms / std::sqrt(energy / (audio.size() * 2));
    double peak = 0;
    for (auto &s : audio) {
        s.l = std::tanh(s.l * gain * 1.15) / 1.15;
        s.r = std::tanh(s.r * gain * 1.15) / 1.15;
        peak = std::max({peak, std::abs(s.l), std::abs(s.r)});
    }
    if (peak > 0.86) for (auto &s : audio) { s.l *= 0.86/peak; s.r *= 0.86/peak; }
    if (!loop) {
        for (int i = 0; i < int(audio.size()); i++) {
            double fade = std::min({1.0, double(i) / 80, double(audio.size()-1-i) / 500});
            audio[i].l *= fade; audio[i].r *= fade;
        }
    }
    std::ofstream out(path, std::ios::binary);
    auto u16 = [&](uint16_t value) { out.put(char(value)); out.put(char(value >> 8)); };
    auto u32 = [&](uint32_t value) { u16(value & 65535); u16(value >> 16); };
    uint32_t size = uint32_t(audio.size() * 4);
    out.write("RIFF", 4); u32(size+36); out.write("WAVEfmt ", 8); u32(16);
    u16(1); u16(2); u32(SR); u32(SR*4); u16(4); u16(16);
    out.write("data", 4); u32(size);
    for (auto s : audio) { u16(int16_t(std::lround(s.l * 32767))); u16(int16_t(std::lround(s.r * 32767))); }
    std::cout << path << " · " << double(audio.size())/SR << " seconds\n";
}

// Shared harmonic grid: Dm9 / Bbmaj7 / Fadd9 / C / Gm9 / A7.
// All three pieces have 128 beats, so transitions can preserve harmonic position.
const std::array<std::array<int,4>, 8> chords = {{{62,65,69,76}, {62,65,69,72},
    {58,62,65,69}, {58,62,65,72}, {60,65,69,72}, {60,64,67,74}, {55,58,62,69}, {57,61,64,67}}};
const std::array<int,8> roots = {38,38,34,34,41,36,43,33};
const std::array<std::array<int,8>,8> tune = {{{74,0,77,76,74,0,69,72},
    {74,77,81,0,79,77,76,0}, {77,0,74,72,70,0,69,65}, {69,70,74,0,77,76,74,0},
    {77,0,81,79,77,0,76,72}, {76,79,84,0,83,79,76,0}, {79,0,77,74,70,0,69,74},
    {73,76,81,79,76,73,69,0}}};

Audio music(int mode) {
    Audio a(int(BARS * 4 * BEAT * SR));
    for (int bar = 0; bar < BARS; bar++) {
        int chord = bar % 8, section = bar / 8;
        double start = bar * 4;
        // A soft bed and arpeggiated keys unite all arrangements.
        for (int i = 0; i < 4; i++) {
            note(a, start + i*0.018, 3.65, chords[chord][i], Pad, mode == 0 ? 0.043 : 0.030, (i-1.5)*0.42);
            note(a, start + i*0.018, 1.7, chords[chord][i], Piano, mode == 0 ? 0.075 : 0.040, (i-1.5)*0.26);
        }
        for (int step = 0; step < 8; step++) {
            int index = std::array<int,8>{0,2,1,3,2,1,3,2}[step];
            note(a, start + step*0.5, 0.32, chords[chord][index]+12, Pluck,
                mode == 0 ? 0.050 : mode == 1 ? 0.038 : 0.046, step%2 ? 0.40 : -0.40);
        }
        // A recurring original eight-bar motif; the menu leaves more breathing space.
        for (int step = 0; step < 8; step++) {
            int pitch = tune[chord][step];
            if (!pitch) continue;
            if (mode == 0 && (step%2 || section == 1 || section == 3 && chord < 4)) continue;
            double hold = step == 7 || tune[chord][std::min(step+1,7)] == 0 ? 0.84 : 0.40;
            if (mode == 0) note(a, start + step*0.5, 0.9, pitch, Piano, 0.115, -0.12);
            else {
                note(a, start + step*0.5, hold, pitch, Lead, mode == 1 ? 0.081 : 0.095, -0.08);
                if (mode == 2 && section != 1) note(a, start + step*0.5, hold, pitch+12, Lead, 0.026, 0.15);
            }
        }
        if (mode == 0) {
            note(a, start, 1.8, roots[chord], Bass, 0.12, 0);
            note(a, start+2, 1.4, roots[chord]+12, Bass, 0.065, 0);
            if (bar%2 == 0) note(a, start+3, 0.3, chords[chord][3]+12, Bell, 0.035, 0.55);
            // Restrained half-time percussion for the common menu theme.
            drum(a, start, Kick, 0.075); drum(a, start+2, Snare, 0.045, -0.18);
            for (int i = 0; i < 4; i++) drum(a, start+i, Hat, 0.025, 0.35);
        } else {
            for (int step = 0; step < 8; step++) {
                int bass = roots[chord] + (step == 5 ? 7 : step%3 == 2 ? 12 : 0);
                note(a, start+step*0.5, 0.36, bass, Bass, mode == 1 ? 0.15 : 0.17, 0);
            }
            for (double beat : {0.0, 1.5, 2.0, 3.25}) drum(a, start+beat, Kick, mode == 1 ? 0.22 : 0.25);
            drum(a, start+1, Snare, 0.15, -0.08); drum(a, start+3, Snare, 0.17, -0.08);
            for (int step = 0; step < (mode == 2 ? 16 : 8); step++)
                drum(a, start+step*(mode == 2 ? 0.25 : 0.5), Hat, step%2 ? 0.035 : 0.055, 0.42);
            if (bar%4 == 3) {
                drum(a, start+3.5, Tom, 0.13, -0.3); drum(a, start+3.75, Tom, 0.11, 0.3);
            }
            if (bar%8 == 0) drum(a, start, Crash, 0.11, -0.40);
            if (mode == 2) {
                // Counter-rhythm, octave strings and a rising sixteenth-note figure.
                for (int i = 0; i < 4; i++) {
                    note(a, start+0.75, 0.34, chords[chord][i]+12, Lead, 0.025, -0.6+i*0.4);
                    note(a, start+2.75, 0.34, chords[chord][i]+12, Lead, 0.025, -0.6+i*0.4);
                }
                for (int step = 0; step < 16; step++)
                    note(a, start+step*0.25, 0.18, chords[chord][step%4]+(step/4%2 ? 24 : 12),
                        Pluck, 0.028, step%2 ? -0.65 : 0.65);
                drum(a, start+2.5, Kick, 0.14);
            }
        }
    }
    space(a, true, mode == 0 ? 0.60 : 0.38);
    return a;
}

Audio effect(int kind) {
    double length = kind == 0 ? 0.22 : kind == 1 ? 1.42 : kind == 2 ? 1.05 : 0.46;
    Audio a(int(length * SR));
    if (kind == 0) {
        double lp = 0;
        for (int i = 0; i < int(a.size()); i++) {
            double t = double(i)/SR, n = noise(); lp += 0.15*(n-lp);
            double swish = std::sin(PI*std::min(1.0,t/0.19));
            double value = (n-lp) * swish*swish * 0.16 + sine(850*t+1300*t*t)*std::exp(-t*45)*0.09;
            add(a, i, value, -0.2+0.4*t/length, false);
        }
    } else if (kind == 1) {
        const double impacts[] = {0.015,0.11,0.205,0.30,0.39,0.49,0.60,0.73,0.88,1.06,1.25};
        for (int k = 0; k < 11; k++) {
            for (int i = 0; i < int(0.085*SR); i++) {
                double t = double(i)/SR;
                double click = (sine((620+k*39)*t)+0.50*sine((1430+k*57)*t))*std::exp(-t*100);
                click += noise()*std::exp(-t*180)*0.55;
                double amp = (0.18+0.16*std::sin(PI*k/10.0))*(k == 10 ? 0.75 : 1);
                add(a, int(impacts[k]*SR)+i, click*amp, k%2 ? -0.18 : 0.18, false);
            }
        }
    } else if (kind == 2) {
        // A short, distinct rising restorative chime, with no voice sample.
        for (int k = 0; k < 5; k++) note(a, k*0.23, 0.28, std::array<int,5>{74,77,81,86,89}[k], Bell, 0.15, (k-2)*0.18, false);
        space(a, false, 0.55);
    } else {
        double lp = 0;
        for (int i = 0; i < int(a.size()); i++) {
            double t = double(i)/SR, n = noise(); lp += 0.08*(n-lp);
            double whoosh = std::pow(std::sin(PI*std::min(1.0,t/0.20)),2);
            double hit = t > 0.12 ? std::exp(-(t-0.12)*23) : 0;
            double value = (n-lp)*whoosh*0.18 + (sine(72*t+0.6*(1-std::exp(-t*45)))+lp)*hit*0.38;
            add(a, i, value, 0, false);
        }
    }
    return a;
}

int main(int argc, char **argv) {
    std::filesystem::path output = argc > 1 ? argv[1] : "/tmp/relick-audio-wav";
    std::filesystem::create_directories(output);
    std::array<std::string,3> songs = {"menu", "yuno-combat", "yuno-climax"};
    for (int i = 0; i < 3; i++) writeWav((output/(songs[i]+".wav")).string(), music(i), true, i == 0 ? 0.15 : 0.18);
    std::array<std::string,4> sounds = {"card-draw", "dice-roll", "heal", "attack"};
    for (int i = 0; i < 4; i++) writeWav((output/(sounds[i]+".wav")).string(), effect(i), false, 0.18);
}
