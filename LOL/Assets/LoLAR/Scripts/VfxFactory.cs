using UnityEngine;

namespace LoLAR
{
    /// <summary>
    /// Crea los sistemas de partículas de la batalla. Todas las medidas están en metros (escala de carta).
    /// Lo usa el constructor de Editor, pero también funciona en runtime.
    /// </summary>
    public static class VfxFactory
    {
        public static ParticleSystem CreateFire(Transform parent, Material material)
        {
            var system = Create("DragonFire", parent, material);
            var main = system.main;
            main.duration = 0.8f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.18f, 0.28f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.008f, 0.018f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.3f), new Color(1f, 0.35f, 0.05f));

            var emission = system.emission;
            emission.rateOverTime = 120f;

            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 14f;
            shape.radius = 0.002f;

            SetFade(system, Color.white, new Color(1f, 0.35f, 0.15f));
            SetSizeCurve(system, 0.6f, 1.8f);
            return system;
        }

        public static ParticleSystem CreateRoarBurst(Transform parent, Material material)
        {
            var system = Create("RoarBurst", parent, material);
            var main = system.main;
            main.duration = 0.6f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.004f, 0.01f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.3f, 0.1f), new Color(1f, 0.75f, 0.2f));

            var emission = system.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)60) });

            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.01f;

            SetFade(system, Color.white, Color.white);
            SetSizeCurve(system, 1f, 0.2f);
            return system;
        }

        public static ParticleSystem CreateHitSparks(Transform parent, Material material)
        {
            var system = Create("HitSparks", parent, material);
            var main = system.main;
            main.duration = 1f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.35f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.003f, 0.007f);
            main.startColor = new ParticleSystem.MinMaxGradient(Color.white, new Color(1f, 0.9f, 0.5f));
            // Se emite con EmitParams en coordenadas de mundo.
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = system.emission;
            emission.rateOverTime = 0f;

            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.003f;

            SetFade(system, Color.white, Color.white);
            SetSizeCurve(system, 1f, 0.1f);
            return system;
        }

        public static ParticleSystem CreateSpawnFlash(Transform parent, Material material)
        {
            var system = Create("SpawnFlash", parent, material);
            var main = system.main;
            main.duration = 0.8f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.003f, 0.008f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.5f, 0.9f, 1f), Color.white);
            main.gravityModifier = -0.05f;

            var emission = system.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)35) });

            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.012f;

            SetFade(system, Color.white, Color.white);
            SetSizeCurve(system, 1f, 0.3f);
            return system;
        }

        static ParticleSystem Create(string name, Transform parent, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var system = go.AddComponent<ParticleSystem>();
            // Duration solo puede cambiarse con el sistema detenido.
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 300;

            var shape = system.shape;
            shape.enabled = true;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            if (material)
                renderer.sharedMaterial = material;

            return system;
        }

        static void SetFade(ParticleSystem system, Color start, Color end)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(start, 0f), new GradientColorKey(end, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });

            var colorOverLifetime = system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);
        }

        static void SetSizeCurve(ParticleSystem system, float from, float to)
        {
            var sizeOverLifetime = system.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, from, 1f, to));
        }
    }
}
