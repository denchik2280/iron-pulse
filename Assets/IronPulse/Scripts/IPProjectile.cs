using UnityEngine;

namespace IronPulse
{
    /// <summary>A readable, dodgeable enemy bolt. Swept collision prevents tunnelling.</summary>
    public sealed class IPProjectile : MonoBehaviour
    {
        IPPlayer player;
        IPEnemy owner;
        Vector3 velocity;
        float damage;
        float life;
        Color tint;
        readonly RaycastHit[] hits = new RaycastHit[24];

        public static IPProjectile Fire(IPPlayer target, IPEnemy source, Vector3 origin, Vector3 velocity, float damage, Color tint)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Hostile plasma — dodgeable";
            IPArt.RemoveCollider(go);
            go.transform.position = origin;
            go.transform.localScale = Vector3.one * .22f;
            go.GetComponent<Renderer>().sharedMaterial = IPArt.Mat("Enemy plasma", tint, 5f);
            var projectile = go.AddComponent<IPProjectile>();
            projectile.player = target;
            projectile.owner = source;
            projectile.velocity = velocity;
            projectile.damage = damage;
            projectile.tint = tint;
            projectile.life = 5f;
            TrailRenderer trail = go.AddComponent<TrailRenderer>();
            trail.sharedMaterial = IPArt.Mat("Plasma trail", tint, 3f);
            trail.time = .16f;
            trail.startWidth = .16f;
            trail.endWidth = .01f;
            trail.minVertexDistance = .07f;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            return projectile;
        }

        void Update()
        {
            if (player == null || player.IsDead || (life -= Time.deltaTime) <= 0f)
            {
                Destroy(gameObject);
                return;
            }
            Vector3 from = transform.position;
            Vector3 step = velocity * Time.deltaTime;
            float distance = step.magnitude;
            if (distance <= .0001f) return;
            int count = Physics.SphereCastNonAlloc(from, .13f, step / distance, hits, distance, ~0, QueryTriggerInteraction.Ignore);
            int nearest = -1;
            float nearestDistance = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                Collider collider = hits[i].collider;
                if (collider == null || collider.transform.IsChildOf(transform)) continue;
                if (owner != null && collider.transform.IsChildOf(owner.transform)) continue;
                // Friendly robots do not unexpectedly absorb a clearly telegraphed projectile.
                if (collider.GetComponentInParent<IPEnemy>() != null) continue;
                if (hits[i].distance < nearestDistance) { nearest = i; nearestDistance = hits[i].distance; }
            }
            if (nearest >= 0)
            {
                RaycastHit hit = hits[nearest];
                IPPlayer victim = hit.collider.GetComponentInParent<IPPlayer>();
                if (victim != null) victim.TakeDamage(damage, from);
                IPGame.Burst(hit.point, tint, 9, 3.2f);
                IPGame.Audio("boltImpact", hit.point, .35f);
                Destroy(gameObject);
                return;
            }
            transform.position = from + step;
        }
    }
}
