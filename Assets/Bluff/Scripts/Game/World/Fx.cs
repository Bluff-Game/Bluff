using System.Collections;
using UnityEngine;

namespace Bluff.Game
{
    /// <summary>
    /// Efek visual prosedural (partikel, kilatan cahaya, gelombang). Semua efek menghapus dirinya sendiri.
    /// </summary>
    public static class Fx
    {
        // ------------------------------------------------------------------ util partikel

        sealed class Spec
        {
            public string Name = "Fx";
            public Material Material;
            public int Burst;
            public float Rate;
            public float Duration = 0.2f;
            public Vector2 Life = new Vector2(0.5f, 1f);
            public Vector2 Speed = new Vector2(0.5f, 1f);
            public Vector2 Size = new Vector2(0.03f, 0.06f);
            public Color ColorA = Color.white;
            public Color ColorB = Color.white;
            public float Gravity;
            public float Radius = 0.02f;
            public float ConeAngle = -1f;
            public bool Stretch;
            public float SizeEnd = 1f;
            public float FadeIn;
            public float Drag;
            public Vector3 Velocity;
            public float Orbit;
            public bool Rotate;
        }

        static ParticleSystem Emit(Vector3 position, Quaternion rotation, Spec s)
        {
            var go = new GameObject(s.Name);
            go.transform.position = position;
            go.transform.rotation = rotation;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = Mathf.Max(0.05f, s.Duration);
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(s.Life.x, s.Life.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(s.Speed.x, s.Speed.y);
            main.startSize = new ParticleSystem.MinMaxCurve(s.Size.x, s.Size.y);
            main.startRotation = s.Rotate ? new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f) : new ParticleSystem.MinMaxCurve(0f);
            main.startColor = new ParticleSystem.MinMaxGradient(s.ColorA, s.ColorB);
            main.gravityModifier = new ParticleSystem.MinMaxCurve(s.Gravity);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 400;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(s.Rate);
            if (s.Burst > 0)
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)s.Burst) });

            var shape = ps.shape;
            shape.enabled = true;
            if (s.ConeAngle >= 0f)
            {
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = s.ConeAngle;
                shape.radius = Mathf.Max(0.001f, s.Radius);
            }
            else
            {
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = Mathf.Max(0.001f, s.Radius);
            }

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                s.FadeIn > 0f
                    ? new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, s.FadeIn), new GradientAlphaKey(0f, 1f) }
                    : new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, s.SizeEnd));

            if (s.Drag > 0f)
            {
                var limit = ps.limitVelocityOverLifetime;
                limit.enabled = true;
                limit.drag = new ParticleSystem.MinMaxCurve(s.Drag);
            }

            if (s.Velocity != Vector3.zero || s.Orbit != 0f)
            {
                var velocity = ps.velocityOverLifetime;
                velocity.enabled = true;
                velocity.space = ParticleSystemSimulationSpace.World;
                velocity.x = new ParticleSystem.MinMaxCurve(s.Velocity.x);
                velocity.y = new ParticleSystem.MinMaxCurve(s.Velocity.y);
                velocity.z = new ParticleSystem.MinMaxCurve(s.Velocity.z);
                velocity.orbitalX = new ParticleSystem.MinMaxCurve(0f);
                velocity.orbitalY = new ParticleSystem.MinMaxCurve(s.Orbit);
                velocity.orbitalZ = new ParticleSystem.MinMaxCurve(0f);
            }

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = s.Material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (s.Stretch)
            {
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.velocityScale = 0.08f;
                renderer.lengthScale = 1.5f;
            }
            else
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
            }

            ps.Play();
            Object.Destroy(go, s.Duration + s.Life.y + 0.5f);
            return ps;
        }

        static Material AddMat(Color c) => Art.Additive(c, Art.SoftTexture);
        static Material SparkMat(Color c) => Art.Additive(c, Art.SparkTexture);
        static Material SmokeMat(Color c) => Art.Unlit(c, Art.SoftTexture);

        // ------------------------------------------------------------------ efek

        /// <summary>Kilatan moncong revolver: cahaya, percikan, dan asap.</summary>
        public static void MuzzleFlash(Vector3 position, Vector3 direction)
        {
            var rot = Quaternion.LookRotation(direction.sqrMagnitude > 1e-6f ? direction : Vector3.forward);
            Flash(position, Theme.Hex("FFC266"), 0.35f, 0.09f);
            PointLight(position, Theme.Hex("FFB347"), 7f, 2.5f, 0.15f);
            Emit(position, rot, new Spec
            {
                Name = "Sparks", Material = SparkMat(Color.white), Burst = 22, Life = new Vector2(0.15f, 0.35f), Speed = new Vector2(2f, 4.5f),
                Size = new Vector2(0.015f, 0.035f), ColorA = Theme.Hex("FFE08A"), ColorB = Theme.Hex("FF8C3A"), ConeAngle = 18f, Radius = 0.005f,
                Stretch = true, Gravity = 0.4f, SizeEnd = 0.2f,
            });
            Emit(position, rot, new Spec
            {
                Name = "Smoke", Material = SmokeMat(Color.white), Burst = 9, Life = new Vector2(0.8f, 1.5f), Speed = new Vector2(0.15f, 0.5f),
                Size = new Vector2(0.06f, 0.12f), ColorA = new Color(0.8f, 0.8f, 0.82f, 0.45f), ColorB = new Color(0.6f, 0.6f, 0.62f, 0.3f),
                ConeAngle = 25f, Radius = 0.01f, Gravity = -0.05f, SizeEnd = 3f, Drag = 1.5f, Rotate = true,
            });
        }

        /// <summary>Jejak peluru dari a ke b.</summary>
        public static void Tracer(Vector3 from, Vector3 to, Color color)
        {
            var go = new GameObject("Tracer");
            var dir = to - from;
            go.transform.position = (from + to) * 0.5f;
            go.transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(0f, 90f, 0f);
            var quad = Art.Quad("Beam", go.transform, Vector3.zero, new Vector2(dir.magnitude, 0.04f), Art.AdditiveInstance(color, Art.StreakTexture));
            var billboard = go.AddComponent<FadeOut>();
            billboard.Init(quad.GetComponent<MeshRenderer>().sharedMaterial, 0.18f, Vector3.one, Vector3.one);
        }

        /// <summary>Kepulan debu kecil saat kartu mendarat di meja.</summary>
        public static void CardDust(Vector3 position)
        {
            Emit(position, Quaternion.identity, new Spec
            {
                Name = "Dust", Material = SmokeMat(Color.white), Burst = 6, Life = new Vector2(0.4f, 0.7f), Speed = new Vector2(0.1f, 0.3f),
                Size = new Vector2(0.03f, 0.06f), ColorA = new Color(1f, 0.95f, 0.85f, 0.35f), ColorB = new Color(1f, 1f, 1f, 0.2f),
                Radius = 0.04f, SizeEnd = 2f, Drag = 3f,
            });
        }

        public static void Confetti(Vector3 position)
        {
            Color[] colors = { Theme.Gold, Theme.Red, Theme.Green, Theme.Hex("63B3ED"), Theme.Hex("F687B3") };
            for (int i = 0; i < colors.Length; i++)
            {
                Emit(position, Quaternion.Euler(-90f, 0f, 0f), new Spec
                {
                    Name = "Confetti", Material = Art.Unlit(Color.white), Burst = 30, Life = new Vector2(2f, 3.2f), Speed = new Vector2(2f, 4f),
                    Size = new Vector2(0.015f, 0.03f), ColorA = colors[i], ColorB = Color.Lerp(colors[i], Color.white, 0.3f), ConeAngle = 35f,
                    Radius = 0.2f, Gravity = 0.6f, Drag = 0.8f, Rotate = true,
                });
            }
        }

        /// <summary>Api yang membakar tumpukan (Hangus / Bakar).</summary>
        public static void Fire(Vector3 position, float duration, float scale = 1f)
        {
            Emit(position, Quaternion.Euler(-90f, 0f, 0f), new Spec
            {
                Name = "Flames", Material = AddMat(Color.white), Rate = 70f * scale, Duration = duration, Life = new Vector2(0.4f, 0.8f),
                Speed = new Vector2(0.2f, 0.5f), Size = new Vector2(0.06f * scale, 0.12f * scale), ColorA = Theme.Hex("FF9A3C"),
                ColorB = Theme.Hex("FF4D2E"), ConeAngle = 10f, Radius = 0.06f * scale, SizeEnd = 0.2f, Gravity = -0.3f,
            });
            Emit(position, Quaternion.Euler(-90f, 0f, 0f), new Spec
            {
                Name = "Embers", Material = SparkMat(Color.white), Rate = 25f * scale, Duration = duration, Life = new Vector2(0.6f, 1.3f),
                Speed = new Vector2(0.3f, 0.8f), Size = new Vector2(0.01f, 0.02f), ColorA = Theme.Hex("FFD27A"), ColorB = Theme.Hex("FF7A2E"),
                ConeAngle = 25f, Radius = 0.05f * scale, Gravity = -0.2f, SizeEnd = 0.3f,
            });
            Emit(position + Vector3.up * 0.15f, Quaternion.Euler(-90f, 0f, 0f), new Spec
            {
                Name = "FireSmoke", Material = SmokeMat(Color.white), Rate = 14f * scale, Duration = duration, Life = new Vector2(1f, 1.8f),
                Speed = new Vector2(0.15f, 0.35f), Size = new Vector2(0.08f, 0.14f), ColorA = new Color(0.2f, 0.18f, 0.18f, 0.35f),
                ColorB = new Color(0.3f, 0.3f, 0.3f, 0.25f), ConeAngle = 15f, Radius = 0.05f, SizeEnd = 3f, FadeIn = 0.2f, Rotate = true,
            });
            PointLight(position + Vector3.up * 0.1f, Theme.Hex("FF8A3C"), 3f, 1.6f, duration + 0.4f);
        }

        /// <summary>Pusaran angin (Badai).</summary>
        public static void Storm(Vector3 position)
        {
            Emit(position, Quaternion.identity, new Spec
            {
                Name = "Storm", Material = SmokeMat(Color.white), Rate = 120f, Duration = 1.1f, Life = new Vector2(0.8f, 1.3f),
                Speed = new Vector2(0.05f, 0.2f), Size = new Vector2(0.05f, 0.1f), ColorA = new Color(0.75f, 0.8f, 1f, 0.45f),
                ColorB = new Color(0.9f, 0.9f, 1f, 0.3f), Radius = 0.35f, Velocity = new Vector3(0f, 0.6f, 0f), Orbit = 6f, SizeEnd = 2f, FadeIn = 0.2f,
            });
            Emit(position, Quaternion.identity, new Spec
            {
                Name = "StormSpark", Material = SparkMat(Color.white), Rate = 40f, Duration = 1f, Life = new Vector2(0.5f, 1f),
                Speed = new Vector2(0.1f, 0.3f), Size = new Vector2(0.015f, 0.03f), ColorA = Theme.Hex("C7D2FE"), ColorB = Color.white,
                Radius = 0.3f, Velocity = new Vector3(0f, 0.8f, 0f), Orbit = 8f,
            });
        }

        /// <summary>Gelembung tameng biru di atas taruhan.</summary>
        public static void Shield(Vector3 position, float radius)
        {
            var go = Art.Primitive(PrimitiveType.Sphere, "ShieldBubble", null, position, Vector3.one * radius * 2f, Art.AdditiveInstance(Theme.Hex("60A5FA").WithAlpha(0.35f), Art.RingTexture));
            go.transform.position = position;
            var fade = go.AddComponent<FadeOut>();
            fade.Init(go.GetComponent<MeshRenderer>().sharedMaterial, 1.6f, Vector3.one * radius * 1.2f, Vector3.one * radius * 2.4f);
            Sparkle(position + Vector3.up * radius * 0.5f, Theme.Hex("93C5FD"), 30, radius);
        }

        public static void MirrorFlash(Vector3 position)
        {
            Flash(position, Color.white, 0.6f, 0.25f);
            Emit(position, Quaternion.identity, new Spec
            {
                Name = "Shards", Material = SparkMat(Color.white), Burst = 40, Life = new Vector2(0.4f, 0.9f), Speed = new Vector2(1f, 2.5f),
                Size = new Vector2(0.02f, 0.05f), ColorA = Color.white, ColorB = Theme.Hex("C4B5FD"), Radius = 0.05f, Gravity = 0.8f, Rotate = true,
            });
        }

        public static void Gift(Vector3 position)
        {
            Sparkle(position, Theme.Hex("F472B6"), 35, 0.12f);
            Sparkle(position, Color.white, 15, 0.12f);
        }

        public static void Peek(Vector3 position)
        {
            Sparkle(position, Theme.Hex("2DD4BF"), 30, 0.15f);
            PointLight(position, Theme.Hex("2DD4BF"), 2f, 1f, 0.6f);
        }

        public static void Sparkle(Vector3 position, Color color, int count, float radius)
        {
            Emit(position, Quaternion.identity, new Spec
            {
                Name = "Sparkle", Material = SparkMat(Color.white), Burst = count, Life = new Vector2(0.5f, 1.1f), Speed = new Vector2(0.1f, 0.5f),
                Size = new Vector2(0.02f, 0.045f), ColorA = color, ColorB = Color.Lerp(color, Color.white, 0.5f), Radius = radius, Gravity = -0.1f,
                SizeEnd = 0f, Rotate = true,
            });
        }

        /// <summary>Tetes keringat (ketahuan bohong).</summary>
        public static void Sweat(Vector3 headPosition)
        {
            Emit(headPosition + Vector3.up * 0.12f, Quaternion.Euler(-90f, 0f, 0f), new Spec
            {
                Name = "Sweat", Material = AddMat(Color.white), Burst = 8, Duration = 0.6f, Rate = 8f, Life = new Vector2(0.6f, 1f),
                Speed = new Vector2(0.4f, 0.8f), Size = new Vector2(0.025f, 0.04f), ColorA = Theme.Hex("7DD3FC"), ColorB = Theme.Hex("BAE6FD"),
                ConeAngle = 40f, Radius = 0.08f, Gravity = 1.2f,
            });
        }

        /// <summary>Gelombang cincin di meja (mis. saat cap BOHONG/JUJUR).</summary>
        public static void Shockwave(Vector3 position, Color color, float size)
        {
            var go = Art.Quad("Shockwave", null, position, Vector2.one, Art.AdditiveInstance(color, Art.RingTexture));
            go.transform.position = position + Vector3.up * 0.003f;
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var fade = go.AddComponent<FadeOut>();
            fade.Init(go.GetComponent<MeshRenderer>().sharedMaterial, 0.6f, Vector3.one * size * 0.2f, Vector3.one * size);
        }

        static void Flash(Vector3 position, Color color, float size, float duration)
        {
            var go = Art.Quad("Flash", null, position, Vector2.one * size, Art.AdditiveInstance(color, Art.SparkTexture));
            go.transform.position = position;
            go.AddComponent<Billboard>();
            var fade = go.AddComponent<FadeOut>();
            fade.Init(go.GetComponent<MeshRenderer>().sharedMaterial, duration, Vector3.one * size, Vector3.one * size * 1.6f);
        }

        public static void PointLight(Vector3 position, Color color, float intensity, float range, float duration)
        {
            var go = new GameObject("FxLight");
            go.transform.position = position;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            go.AddComponent<FadeLight>().Init(intensity, duration);
        }

        // ------------------------------------------------------------------ komponen kecil

        /// <summary>Memudar sambil membesar, lalu hilang.</summary>
        public sealed class FadeOut : MonoBehaviour
        {
            Material material;
            float duration;
            float time;
            Vector3 from;
            Vector3 to;
            Color baseColor;

            public void Init(Material mat, float seconds, Vector3 scaleFrom, Vector3 scaleTo)
            {
                material = mat;
                duration = Mathf.Max(0.01f, seconds);
                from = scaleFrom;
                to = scaleTo;
                baseColor = mat.GetColor("_Color");
                transform.localScale = scaleFrom;
            }

            void Update()
            {
                time += Time.deltaTime;
                float t = Mathf.Clamp01(time / duration);
                transform.localScale = Vector3.Lerp(from, to, Tween.EaseOutCubic(t));
                if (material != null)
                    material.SetColor("_Color", baseColor.WithAlpha(baseColor.a * (1f - t)));
                if (t >= 1f)
                    Destroy(gameObject);
            }

            void OnDestroy()
            {
                if (material != null)
                    Destroy(material);
            }
        }

        public sealed class FadeLight : MonoBehaviour
        {
            Light target;
            float start;
            float duration;
            float time;

            public void Init(float intensity, float seconds)
            {
                target = GetComponent<Light>();
                start = intensity;
                duration = Mathf.Max(0.01f, seconds);
            }

            void Update()
            {
                time += Time.deltaTime;
                float t = Mathf.Clamp01(time / duration);
                if (target != null)
                    target.intensity = start * (1f - t) * (1f - t);
                if (t >= 1f)
                    Destroy(gameObject);
            }
        }

        /// <summary>Selalu menghadap kamera utama.</summary>
        public sealed class Billboard : MonoBehaviour
        {
            void LateUpdate()
            {
                var cam = Camera.main;
                if (cam != null)
                    transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
            }
        }
    }
}
