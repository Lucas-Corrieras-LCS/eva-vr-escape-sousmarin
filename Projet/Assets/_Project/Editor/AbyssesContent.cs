using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Abysses.EditorTools
{
    /// <summary>
    /// Contenu procédural du projet : sons (WAV) et textures (PNG) générés par code.
    /// Reproductible (graine fixe), aucun asset externe ni licence à gérer. Relancé par <see cref="AbyssesBuilder.BuildAll"/>.
    /// </summary>
    public static class AbyssesContent
    {
        const string AudioDir = "Assets/_Project/Audio";
        const string ArtDir = "Assets/_Project/Art";
        const int Rate = 22050;
        const float Tau = Mathf.PI * 2f;

        public const int CausticFrames = 16;
        /// <summary>Formes du digicode, dans l'ordre des symboles (ROUGE, VERT, BLEU, JAUNE, BLANC, VIOLET).</summary>
        public static readonly string[] GlyphNames = { "Cercle", "Triangle", "Carre", "Etoile", "Losange", "Croix" };

        public class Content
        {
            public AudioClip AmbienceStation, AmbienceHabitat, AmbienceLabo, AmbienceCoeur;
            public AudioClip Bubbles, Rumble, ZoneAlarm, Key, Error, Success, FuseInsert, Hatch, LeverFail, ReactorTone, ReactorFail, FloorSolved, Finale;
            public Texture2D[] Caustics;
            public Texture2D[] Glyphs;
            public Texture2D Dot, ShaftGradient;
            public Mesh ShaftCone;
        }

        public static Content Build()
        {
            EnsureFolder(AudioDir);
            EnsureFolder(ArtDir);
            var c = new Content
            {
                AmbienceStation = Loop("AMB_Station", 12f, AmbienceStation),
                AmbienceHabitat = Loop("AMB_Habitat", 16f, AmbienceHabitat),
                AmbienceLabo = Loop("AMB_Labo", 16f, AmbienceLabo),
                AmbienceCoeur = Loop("AMB_Coeur", 16f, AmbienceCoeur),
                Rumble = Loop("SFX_Rumble", 6f, Rumble),
                ZoneAlarm = Loop("SFX_ZoneAlarm", 2f, ZoneAlarm),
                Bubbles = OneShot("SFX_Bubbles", 1.4f, Bubbles),
                Key = OneShot("SFX_Key", 0.12f, Key),
                Error = OneShot("SFX_Error", 0.6f, Error),
                Success = OneShot("SFX_Success", 1.2f, Success),
                FuseInsert = OneShot("SFX_FuseInsert", 0.9f, FuseInsert),
                Hatch = OneShot("SFX_Hatch", 1.6f, Hatch),
                LeverFail = OneShot("SFX_LeverFail", 1.2f, LeverFail),
                ReactorTone = OneShot("SFX_ReactorTone", 0.9f, ReactorTone),
                ReactorFail = OneShot("SFX_ReactorFail", 0.9f, ReactorFail),
                FloorSolved = OneShot("SFX_FloorSolved", 2.5f, FloorSolved),
                Finale = OneShot("SFX_Finale", 8f, Finale),
            };

            c.Caustics = new Texture2D[CausticFrames];
            for (int i = 0; i < CausticFrames; i++)
                c.Caustics[i] = Png("T_Caustics_" + i.ToString("00"), CausticsFrame(128, i / (float)CausticFrames), 128, 128, true, false);
            c.Glyphs = new Texture2D[GlyphNames.Length];
            for (int i = 0; i < GlyphNames.Length; i++)
                c.Glyphs[i] = Png("T_Glyph_" + GlyphNames[i], Glyph(128, i), 128, 128, false, true);
            c.Dot = Png("T_Dot", SoftDot(64), 64, 64, false, true);
            c.ShaftGradient = Png("T_ShaftGradient", ShaftGradient(8, 128), 8, 128, false, true);
            c.ShaftCone = ShaftConeMesh();
            return c;
        }

        // ------------------------------------------------------------------ Sons

        delegate float Synth(float t, float duration, System.Random random);

        /// <summary>Boucle sans couture : on synthétise un peu plus long et on fond la queue dans le début.</summary>
        static AudioClip Loop(string name, float duration, Synth synth)
        {
            int length = Mathf.RoundToInt(duration * Rate);
            int fade = Rate / 2;
            float[] raw = Render(length + fade, duration, synth);
            var samples = new float[length];
            for (int i = 0; i < length; i++)
            {
                samples[i] = raw[i];
                if (i < fade)
                {
                    float k = i / (float)fade;
                    samples[i] = raw[i] * k + raw[length + i] * (1f - k);
                }
            }
            return SaveWav(name, samples, true);
        }

        static AudioClip OneShot(string name, float duration, Synth synth)
        {
            float[] samples = Render(Mathf.RoundToInt(duration * Rate), duration, synth);
            // Micro-fondus pour éviter les clics.
            int edge = Mathf.Min(64, samples.Length / 4);
            for (int i = 0; i < edge; i++)
            {
                float k = i / (float)edge;
                samples[i] *= k;
                samples[samples.Length - 1 - i] *= k;
            }
            return SaveWav(name, samples, false);
        }

        static float[] Render(int count, float duration, Synth synth)
        {
            var random = new System.Random(SeedOf(synth.Method.Name));
            var samples = new float[count];
            float peak = 0.0001f;
            for (int i = 0; i < count; i++)
            {
                samples[i] = synth(i / (float)Rate, duration, random);
                peak = Mathf.Max(peak, Mathf.Abs(samples[i]));
            }
            // Normalisation à -3 dB.
            float gain = 0.7f / peak;
            for (int i = 0; i < count; i++) samples[i] *= gain;
            return samples;
        }

        /// <summary>Graine stable (string.GetHashCode n'est pas garanti identique d'une exécution à l'autre).</summary>
        static int SeedOf(string text)
        {
            int seed = 17;
            foreach (char ch in text) seed = seed * 31 + ch;
            return seed;
        }

        // État des filtres (les synthés sont appelés échantillon par échantillon, dans l'ordre).
        static float lp1, lp2, lp3, bp1, bp2;

        static float Brown(System.Random r, ref float state, float amount)
        {
            state = Mathf.Clamp(state + ((float)r.NextDouble() * 2f - 1f) * amount, -1f, 1f);
            state *= 0.998f;
            return state;
        }

        static float White(System.Random r) => (float)r.NextDouble() * 2f - 1f;

        static float LowPass(ref float state, float input, float k)
        {
            state += (input - state) * k;
            return state;
        }

        static float Env(float t, float attack, float decay) => t < attack ? t / attack : Mathf.Exp(-(t - attack) / decay);

        /// <summary>Une bulle : sinus dont la fréquence monte, très courte.</summary>
        static float Bubble(float t, float start, float freq)
        {
            float u = t - start;
            if (u < 0f || u > 0.08f) return 0f;
            float f = freq * (1f + u * 18f);
            return Mathf.Sin(Tau * f * u) * Mathf.Exp(-u * 55f);
        }

        // Positions de bulles tirées une fois par son (reproductibles).
        static float[] BubbleStarts(int seed, int count, float duration, float minFreq, float maxFreq, out float[] freqs)
        {
            var r = new System.Random(seed);
            var starts = new float[count];
            freqs = new float[count];
            for (int i = 0; i < count; i++)
            {
                starts[i] = (float)r.NextDouble() * duration;
                freqs[i] = Mathf.Lerp(minFreq, maxFreq, (float)r.NextDouble());
            }
            return starts;
        }

        static float BubbleField(float t, float[] starts, float[] freqs)
        {
            float s = 0f;
            for (int i = 0; i < starts.Length; i++) s += Bubble(t, starts[i], freqs[i]);
            return s;
        }

        static float[] stationBubbles, stationFreqs, habitatBubbles, habitatFreqs, sfxBubbles, sfxFreqs, hatchBubbles, hatchFreqs, finaleBubbles, finaleFreqs;

        // Les fréquences des boucles sont des multiples de 1/durée : la boucle reste juste même sans le fondu.
        static float AmbienceStation(float t, float d, System.Random r)
        {
            if (t == 0f) { lp1 = lp2 = 0f; stationBubbles = BubbleStarts(11, 18, d, 300f, 900f, out stationFreqs); }
            float hum = 0.35f * Mathf.Sin(Tau * 50f * t) + 0.15f * Mathf.Sin(Tau * 100f * t) + 0.25f * Mathf.Sin(Tau * 36f * t);
            float water = LowPass(ref lp1, Brown(r, ref lp2, 0.02f), 0.05f) * 6f;
            float ping = SonarPing(t, 3f, 1150f) + 0.35f * SonarPing(t, 3.45f, 1150f);
            return hum * 0.4f + water * 0.5f + ping * 0.25f + BubbleField(t, stationBubbles, stationFreqs) * 0.08f;
        }

        static float SonarPing(float t, float at, float freq)
        {
            float u = t - at;
            if (u < 0f || u > 2.5f) return 0f;
            return Mathf.Sin(Tau * freq * u) * Env(u, 0.005f, 0.45f);
        }

        static readonly float[] CreakTimes = { 4.2f, 11.5f };
        static readonly float[] FinaleNotes = { 130.81f, 196f, 261.63f, 329.63f, 392f, 523.25f };

        static float AmbienceHabitat(float t, float d, System.Random r)
        {
            if (t == 0f) { lp1 = lp2 = lp3 = 0f; habitatBubbles = BubbleStarts(21, 40, d, 250f, 800f, out habitatFreqs); }
            float swell = 0.6f + 0.4f * Mathf.Sin(Tau * t / d * 2f);
            float drone = Mathf.Sin(Tau * 55f * t) * 0.5f + Mathf.Sin(Tau * 82.5f * t) * 0.25f * swell + Mathf.Sin(Tau * 110.0625f * t) * 0.1f;
            float water = LowPass(ref lp1, Brown(r, ref lp2, 0.03f), 0.04f) * 7f * swell;
            // Grincement lointain de la coque, deux fois par boucle.
            float creak = 0f;
            foreach (float at in CreakTimes)
            {
                float u = t - at;
                if (u > 0f && u < 1.4f)
                    creak += LowPass(ref lp3, White(r), 0.12f) * Mathf.Sin(Tau * (90f + 40f * u) * u) * Env(u, 0.3f, 0.4f);
            }
            return drone * 0.35f + water * 0.6f + creak * 0.9f + BubbleField(t, habitatBubbles, habitatFreqs) * 0.12f;
        }

        static float AmbienceLabo(float t, float d, System.Random r)
        {
            if (t == 0f) { lp1 = lp2 = 0f; }
            float drone = Mathf.Sin(Tau * 41.25f * t) * 0.5f + Mathf.Sin(Tau * 61.875f * t) * 0.2f;
            float electric = (Mathf.Sin(Tau * 50f * t) + 0.5f * Mathf.Sin(Tau * 150f * t) + 0.25f * Mathf.Sin(Tau * 250f * t)) * 0.12f;
            float water = LowPass(ref lp1, Brown(r, ref lp2, 0.03f), 0.03f) * 6f;
            // Alarme de secours très lointaine : deux tons, toutes les 4 s.
            float phase = t % 4f;
            float alarm = phase < 1.2f ? Mathf.Sin(Tau * (phase < 0.6f ? 440f : 330f) * t) * Env(phase % 0.6f, 0.05f, 0.25f) : 0f;
            return drone * 0.4f + electric + water * 0.55f + alarm * 0.08f;
        }

        static float AmbienceCoeur(float t, float d, System.Random r)
        {
            if (t == 0f) { lp1 = lp2 = 0f; }
            float drone = Mathf.Sin(Tau * 32.5f * t) * 0.6f + Mathf.Sin(Tau * 48.75f * t) * 0.3f;
            // Nappe aiguë qui scintille (bioluminescence).
            float shimmer = 0f;
            for (int k = 0; k < 4; k++)
            {
                float f = 520f + 130f * k;
                shimmer += Mathf.Sin(Tau * f * t) * (0.5f + 0.5f * Mathf.Sin(Tau * t / d * (k + 1) + k));
            }
            float water = LowPass(ref lp1, Brown(r, ref lp2, 0.02f), 0.025f) * 6f;
            // Chant lointain (glissando lent, façon baleine).
            float u = t - 6f;
            float whale = u > 0f && u < 4f ? Mathf.Sin(Tau * (180f * u + 25f * u * u)) * Mathf.Sin(Mathf.PI * u / 4f) : 0f;
            float ping = SonarPing(t, 1.5f, 900f) + SonarPing(t, 10f, 900f) * 0.6f;
            return drone * 0.4f + shimmer * 0.025f + water * 0.5f + whale * 0.12f + ping * 0.15f;
        }

        static float Rumble(float t, float d, System.Random r)
        {
            if (t == 0f) { lp1 = lp2 = lp3 = 0f; }
            float wobble = 1f + 0.1f * Mathf.Sin(Tau * t / d * 3f);
            float low = LowPass(ref lp1, Brown(r, ref lp2, 0.05f), 0.08f) * 8f * wobble;
            float motor = Mathf.Sin(Tau * 35f * t) * 0.4f + Mathf.Sin(Tau * 70f * t) * 0.2f + Mathf.Sin(Tau * 120f * t) * 0.06f;
            float rattle = LowPass(ref lp3, White(r), 0.3f) * (0.5f + 0.5f * Mathf.Sin(Tau * 7f * t)) * 0.1f;
            return low * 0.6f + motor * 0.5f + rattle;
        }

        static float ZoneAlarm(float t, float d, System.Random r)
        {
            if (t == 0f) { lp1 = lp2 = 0f; }
            // Coque qui gémit (bruit filtré résonant) + bip d'alerte de pression.
            float groan = LowPass(ref lp1, White(r), 0.05f) * Mathf.Sin(Tau * (70f + 10f * Mathf.Sin(Tau * t / d)) * t) * 4f;
            float phase = t % 0.5f;
            float beep = phase < 0.18f ? Mathf.Sign(Mathf.Sin(Tau * 660f * t)) * 0.3f * Env(phase, 0.01f, 0.1f) : 0f;
            return groan * 0.6f + beep;
        }

        static float Bubbles(float t, float d, System.Random r)
        {
            if (t == 0f) sfxBubbles = BubbleStarts(31, 45, d * 0.8f, 350f, 1300f, out sfxFreqs);
            return BubbleField(t, sfxBubbles, sfxFreqs) * (1f - t / d);
        }

        static float Key(float t, float d, System.Random r)
        {
            float click = t < 0.004f ? White(r) * (1f - t / 0.004f) : 0f;
            return click * 0.5f + Mathf.Sin(Tau * 1320f * t) * Env(t, 0.002f, 0.03f);
        }

        static float Error(float t, float d, System.Random r)
        {
            float phase = t % 0.25f;
            if (t > 0.5f || phase > 0.18f) return 0f;
            float wave = Mathf.Sign(Mathf.Sin(Tau * 150f * t)) * 0.6f + Mathf.Sin(Tau * 75f * t) * 0.4f;
            return wave * Env(phase, 0.005f, 0.12f);
        }

        static float Chime(float t, float start, float freq, float decay)
        {
            float u = t - start;
            if (u < 0f) return 0f;
            float e = Env(u, 0.004f, decay);
            return (Mathf.Sin(Tau * freq * u) + 0.4f * Mathf.Sin(Tau * freq * 2f * u) + 0.15f * Mathf.Sin(Tau * freq * 3.01f * u)) * e;
        }

        static float Success(float t, float d, System.Random r)
        {
            // Arpège montant do-mi-sol-do.
            return Chime(t, 0f, 523.25f, 0.4f) + Chime(t, 0.1f, 659.25f, 0.4f) + Chime(t, 0.2f, 783.99f, 0.45f) + Chime(t, 0.3f, 1046.5f, 0.6f);
        }

        static float FuseInsert(float t, float d, System.Random r)
        {
            if (t == 0f) lp1 = 0f;
            float thump = Mathf.Sin(Tau * (120f - 60f * t) * t) * Env(t, 0.002f, 0.06f);
            float click = LowPass(ref lp1, White(r), 0.6f) * Env(t, 0.001f, 0.015f);
            // Montée en tension électrique.
            float u = t - 0.12f;
            float hum = u > 0f ? Mathf.Sin(Tau * (100f + 200f * Mathf.Min(u, 0.4f)) * u) * Mathf.Min(1f, u * 6f) * Env(u, 0.3f, 0.25f) * 0.5f : 0f;
            return thump + click * 0.7f + hum;
        }

        static float Hatch(float t, float d, System.Random r)
        {
            if (t == 0f) { bp1 = bp2 = 0f; hatchBubbles = BubbleStarts(41, 25, d, 400f, 1200f, out hatchFreqs); }
            // Souffle pneumatique : bruit filtré dont la bande descend.
            float k = Mathf.Lerp(0.5f, 0.05f, t / d);
            float hp = White(r) - LowPass(ref bp1, White(r), 0.02f);
            float whoosh = LowPass(ref bp2, hp, k) * Env(t, 0.08f, 0.35f);
            return whoosh * 1.5f + BubbleField(t, hatchBubbles, hatchFreqs) * 0.3f;
        }

        static float LeverFail(float t, float d, System.Random r)
        {
            // Chute de tension : sinus qui descend + bourdonnement.
            float f = Mathf.Lerp(380f, 60f, Mathf.Sqrt(t / d));
            return (Mathf.Sin(Tau * f * t) * 0.6f + Mathf.Sign(Mathf.Sin(Tau * f * 0.5f * t)) * 0.2f) * Env(t, 0.01f, 0.5f);
        }

        static float ReactorTone(float t, float d, System.Random r) => Chime(t, 0f, 440f, 0.35f) + 0.3f * Chime(t, 0f, 880f * 1.002f, 0.2f);

        static float ReactorFail(float t, float d, System.Random r)
        {
            // Intervalle dissonant (triton).
            return (Mathf.Sin(Tau * 233f * t) + Mathf.Sin(Tau * 329.6f * t) + 0.5f * Mathf.Sign(Mathf.Sin(Tau * 116.5f * t))) * Env(t, 0.01f, 0.35f);
        }

        static float FloorSolved(float t, float d, System.Random r)
        {
            // Remise sous tension : montée d'un accord + « clac » final.
            float rise = Mathf.Clamp01(t / 1.2f);
            float f = Mathf.Lerp(110f, 220f, rise * rise);
            float sweep = (Mathf.Sin(Tau * f * t) + 0.5f * Mathf.Sin(Tau * f * 1.5f * t) + 0.3f * Mathf.Sin(Tau * f * 2f * t)) * Mathf.Min(1f, t * 3f) * (t < 1.2f ? 1f : Mathf.Exp(-(t - 1.2f) * 4f));
            return sweep * 0.5f + Chime(t, 1.2f, 523.25f, 0.6f) + Chime(t, 1.2f, 783.99f, 0.6f) * 0.7f;
        }

        static float Finale(float t, float d, System.Random r)
        {
            if (t == 0f) finaleBubbles = BubbleStarts(51, 120, d, 300f, 1500f, out finaleFreqs);
            // Grand accord majeur qui s'ouvre (la lumière revient) + bulles.
            float k = Mathf.SmoothStep(0f, 1f, t / (d * 0.6f)) * (t > d * 0.8f ? Mathf.Clamp01((d - t) / (d * 0.2f)) : 1f);
            float pad = 0f;
            for (int i = 0; i < FinaleNotes.Length; i++)
                pad += Mathf.Sin(Tau * FinaleNotes[i] * t + 0.3f * Mathf.Sin(Tau * 0.2f * t + i)) / (1f + i * 0.4f);
            return pad * k * 0.5f + BubbleField(t, finaleBubbles, finaleFreqs) * 0.2f;
        }

        static AudioClip SaveWav(string name, float[] samples, bool loop)
        {
            string path = AudioDir + "/" + name + ".wav";
            using (var stream = new FileStream(path, FileMode.Create))
            using (var w = new BinaryWriter(stream))
            {
                int dataSize = samples.Length * 2;
                w.Write(new[] { 'R', 'I', 'F', 'F' });
                w.Write(36 + dataSize);
                w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
                w.Write(16);
                w.Write((short)1);
                w.Write((short)1);
                w.Write(Rate);
                w.Write(Rate * 2);
                w.Write((short)2);
                w.Write((short)16);
                w.Write(new[] { 'd', 'a', 't', 'a' });
                w.Write(dataSize);
                for (int i = 0; i < samples.Length; i++)
                    w.Write((short)Mathf.RoundToInt(Mathf.Clamp(samples[i], -1f, 1f) * short.MaxValue));
            }

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            importer.forceToMono = true;
            importer.loadInBackground = loop;
            // Boucles longues : Vorbis compressé en mémoire. Effets courts : ADPCM décompressé (latence minimale).
            importer.defaultSampleSettings = new AudioImporterSampleSettings
            {
                loadType = loop ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad,
                compressionFormat = loop ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.ADPCM,
                quality = 0.6f,
                sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate,
            };
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        // ------------------------------------------------------------------ Textures

        /// <summary>
        /// Caustiques : bords des cellules de Voronoï (F2 - F1), points qui tournent sur de petits cercles.
        /// Pavable (distances calculées sur un tore) et cyclique dans le temps (phase de 0 à 1).
        /// </summary>
        static Color[] CausticsFrame(int size, float phase)
        {
            const int cells = 5;
            var r = new System.Random(7);
            var points = new Vector2[cells * cells];
            for (int i = 0; i < points.Length; i++)
            {
                var basePoint = new Vector2((i % cells + (float)r.NextDouble()) / cells, (i / cells + (float)r.NextDouble()) / cells);
                float angle = Tau * (phase * (r.Next(2) == 0 ? 1 : -1) + (float)r.NextDouble());
                points[i] = basePoint + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (0.35f / cells);
            }

            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
                float f1 = 9f, f2 = 9f;
                for (int i = 0; i < points.Length; i++)
                {
                    float dx = Mathf.Abs(p.x - points[i].x); dx = Mathf.Min(dx, 1f - dx);
                    float dy = Mathf.Abs(p.y - points[i].y); dy = Mathf.Min(dy, 1f - dy);
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dist < f1) { f2 = f1; f1 = dist; }
                    else if (dist < f2) f2 = dist;
                }
                float edge = (f2 - f1) * cells;
                float v = Mathf.Pow(Mathf.Clamp01(1f - edge * 2.2f), 5f);
                v = Mathf.Clamp01(0.08f + v * 1.1f);
                pixels[y * size + x] = new Color(v, v, v, v);
            }
            return pixels;
        }

        static Color[] Glyph(int size, int shape)
        {
            const int super = 4;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int inside = 0;
                for (int sy = 0; sy < super; sy++)
                for (int sx = 0; sx < super; sx++)
                {
                    // Coordonnées centrées dans [-1, 1].
                    float u = ((x + (sx + 0.5f) / super) / size) * 2f - 1f;
                    float v = ((y + (sy + 0.5f) / super) / size) * 2f - 1f;
                    if (InsideGlyph(shape, u, v)) inside++;
                }
                float a = inside / (float)(super * super);
                pixels[y * size + x] = new Color(1f, 1f, 1f, a);
            }
            return pixels;
        }

        static bool InsideGlyph(int shape, float u, float v)
        {
            const float s = 0.8f;
            switch (shape)
            {
                case 0: return u * u + v * v <= s * s;
                case 1: return v >= -0.62f && Mathf.Abs(u) <= (0.78f - v) * 0.62f && v <= 0.78f;
                case 2: return Mathf.Abs(u) <= 0.66f && Mathf.Abs(v) <= 0.66f;
                case 3: return InsideStar(u, v + 0.06f, 0.88f, 0.38f);
                case 4: return Mathf.Abs(u) / 0.68f + Mathf.Abs(v) / 0.88f <= 1f;
                case 5: return (Mathf.Abs(u) <= 0.26f && Mathf.Abs(v) <= 0.82f) || (Mathf.Abs(v) <= 0.26f && Mathf.Abs(u) <= 0.82f);
                default: return false;
            }
        }

        static bool InsideStar(float u, float v, float outer, float inner)
        {
            var poly = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float angle = Mathf.PI / 2f + i * Mathf.PI / 5f;
                float radius = i % 2 == 0 ? outer : inner;
                poly[i] = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
            }
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                if ((poly[i].y > v) != (poly[j].y > v) &&
                    u < (poly[j].x - poly[i].x) * (v - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            }
            return inside;
        }

        static Color[] SoftDot(int size)
        {
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                float a = Mathf.Exp(-(u * u + v * v) * 5f) * Mathf.Clamp01(1f - Mathf.Sqrt(u * u + v * v));
                pixels[y * size + x] = new Color(1f, 1f, 1f, a);
            }
            return pixels;
        }

        /// <summary>Dégradé vertical du rayon de lumière : intense au sommet (v = 1), nul en bas.</summary>
        static Color[] ShaftGradient(int width, int height)
        {
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                float v = y / (float)(height - 1);
                float a = Mathf.Pow(v, 1.6f) * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - v) * 12f));
                for (int x = 0; x < width; x++) pixels[y * width + x] = new Color(1f, 1f, 1f, a);
            }
            return pixels;
        }

        static Texture2D Png(string name, Color[] pixels, int width, int height, bool linearGray, bool clamp)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            texture.Apply();
            string path = ArtDir + "/" + name + ".png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = !linearGray;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = !linearGray;
            importer.wrapMode = clamp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 256;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ------------------------------------------------------------------ Maillage

        /// <summary>Cône ouvert (sans base) : sommet en (0,0,0), base de rayon 1 en y = -1. UV v = 1 au sommet.</summary>
        static Mesh ShaftConeMesh()
        {
            const string path = ArtDir + "/ShaftCone.asset";
            const int segments = 24;
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool created = mesh == null;
            if (created) mesh = new Mesh { name = "ShaftCone" };
            mesh.Clear();

            var vertices = new Vector3[(segments + 1) * 2];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[segments * 6];
            for (int i = 0; i <= segments; i++)
            {
                float angle = Tau * i / segments;
                var ring = new Vector3(Mathf.Cos(angle), -1f, Mathf.Sin(angle));
                vertices[i * 2] = new Vector3(ring.x * 0.08f, 0f, ring.z * 0.08f);
                vertices[i * 2 + 1] = ring;
                uvs[i * 2] = new Vector2(i / (float)segments, 1f);
                uvs[i * 2 + 1] = new Vector2(i / (float)segments, 0f);
                if (i == segments) break;
                int t = i * 6, v = i * 2;
                triangles[t] = v; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
                triangles[t + 3] = v + 1; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
            }
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            if (created) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
