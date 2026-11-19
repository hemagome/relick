// Synthesized Relick effects. The score lives in score.cpp; recorded dice are processed separately.
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

// Bell voice for the restorative chime.
void bell(Audio &a, double beat, double beats, int pitch, double gain, double pan) {
    double f = hz(pitch), duration = beats * BEAT, release = 0.65;
    int begin = std::lround(beat * BEAT * SR), count = int((duration + release) * SR);
    for (int i = 0; i < count; i++) {
        double t = double(i) / SR, p = f * t, envelope = std::min(1.0, t / 0.003);
        if (t > duration) envelope *= std::pow(std::max(0.0, 1 - (t - duration) / release), 2);
        double signal = sine(p) * std::exp(-t * 2.8) + 0.35 * sine(p * 2.756) * std::exp(-t * 5) +
            0.16 * sine(p * 5.405) * std::exp(-t * 9);
        add(a, begin + i, signal * envelope * gain, pan, false);
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

Audio effect(int kind) {
    double length = kind == 0 ? 0.22 : kind == 1 ? 1.05 : 0.46;
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
        // A short, distinct rising restorative chime, with no voice sample.
        for (int k = 0; k < 5; k++) bell(a, k*0.23, 0.28, std::array<int,5>{74,77,81,86,89}[k], 0.15, (k-2)*0.18);
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

Audio deckSaved() {
    Audio audio(int(0.39 * SR));
    // A dotted pickup and a two-voice finish, played with band-limited pulse waves.
    struct Note { double start, duration; int pitch; double gain; int duty; };
    for (const auto &note : std::array<Note, 5>{{
            {0.000, 0.080, 76, 0.72, 4}, {0.090, 0.043, 81, 0.68, 4},
            {0.142, 0.049, 83, 0.65, 4}, {0.202, 0.163, 88, 0.74, 2},
            {0.202, 0.157, 81, 0.32, 4}}}) {
        const double frequency = hz(note.pitch);
        const int start = std::lround(note.start * SR);
        const int count = std::lround(note.duration * SR);
        for (int i = 0; i < count; i++) {
            const double time = double(i) / SR;
            double pulse = 0;
            for (int harmonic = 1; harmonic <= 7; harmonic++) {
                pulse += sine(frequency * time * harmonic) * std::sin(PI * harmonic / note.duty) / harmonic;
            }
            const double envelope = std::min({1.0, time / 0.003, (note.duration - time) / 0.026}) * std::exp(-time * 3.5);
            add(audio, start + i, pulse * envelope * note.gain, 0, false);
        }
    }
    return audio;
}

Audio openingLife() {
    Audio audio(int(0.105 * SR));
    const double pitch = hz(78);
    for (int i = 0; i < int(audio.size()); i++) {
        const double t = double(i) / SR;
        const double phase = pitch * t + 24 * t * t;
        const double pulse = sine(phase) + 0.36 * sine(phase * 3) + 0.14 * sine(phase * 5);
        const double envelope = std::min(1.0, t / .004) * std::exp(-t * 54);
        add(audio, i, pulse * envelope, 0, false);
    }
    return audio;
}

int main(int argc, char **argv) {
    std::filesystem::path output = argc > 1 ? argv[1] : "/tmp/relick-audio-wav";
    std::filesystem::create_directories(output);
    // The generator once rendered the score first; skipping its draws keeps the effects identical.
    rng.discard(8184512);
    std::array<std::string,3> sounds = {"card-draw", "heal", "attack"};
    for (int i = 0; i < 3; i++) writeWav((output/(sounds[i]+".wav")).string(), effect(i), false, 0.18);
    writeWav((output / "deck-saved.wav").string(), deckSaved(), false, 0.14);
    writeWav((output / "opening-life.wav").string(), openingLife(), false, 0.095);
}
