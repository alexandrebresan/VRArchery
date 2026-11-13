using UnityEngine;
using VRArchery.Targets;

namespace VRArchery.Scoring
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public sealed class ScoringTarget : MonoBehaviour
    {
        [SerializeField, Min(0.001f)] float m_YellowRadius = 0.16f;
        [SerializeField, Min(0.001f)] float m_RedRadius = 0.32f;
        [SerializeField, Min(0.001f)] float m_BlueRadius = 0.48f;
        [SerializeField, Min(0.001f)] float m_BlackRadius = 0.64f;
        [SerializeField, Min(0.001f)] float m_WhiteRadius = 0.80f;

        Mesh runtimeMesh;

        public int ScoreAt(Vector3 worldPoint)
            => ScoreLocal(transform.InverseTransformPoint(worldPoint));

        public int ScoreImpactAt(Vector3 worldPoint)
        {
            // Interpolation lags the collider's physical translation by a fraction of a step.
            if (TryGetComponent<MovingTarget>(out var motion)) worldPoint += transform.position - motion.PhysicsPosition;
            return ScoreAt(worldPoint);
        }

        int ScoreLocal(Vector3 local)
        {
            // A pontuação vem da distância radial no plano local da face do alvo.
            float radius = new Vector2(local.x, local.y).magnitude;
            // Allow for round-off when transforming exact ring boundaries back to local space.
            const float tolerance = 0.000001f;
            if (radius <= m_YellowRadius + tolerance) return 10;
            if (radius <= m_RedRadius + tolerance) return 8;
            if (radius <= m_BlueRadius + tolerance) return 6;
            if (radius <= m_BlackRadius + tolerance) return 4;
            return radius <= m_WhiteRadius + tolerance ? 2 : 0;
        }

        void Awake()
        {
            runtimeMesh = CreateFaceMesh();
            GetComponent<MeshFilter>().sharedMesh = runtimeMesh;
            GetComponent<MeshCollider>().sharedMesh = runtimeMesh;
        }

        void OnDestroy()
        {
            if (runtimeMesh != null) Destroy(runtimeMesh);
        }

        void OnValidate()
        {
            const float minimumWidth = 0.001f;
            m_YellowRadius = Mathf.Max(minimumWidth, m_YellowRadius);
            m_RedRadius = Mathf.Max(m_YellowRadius + minimumWidth, m_RedRadius);
            m_BlueRadius = Mathf.Max(m_RedRadius + minimumWidth, m_BlueRadius);
            m_BlackRadius = Mathf.Max(m_BlueRadius + minimumWidth, m_BlackRadius);
            m_WhiteRadius = Mathf.Max(m_BlackRadius + minimumWidth, m_WhiteRadius);
        }

        public Mesh CreateFaceMesh()
        {
            const int sides = 128;
            float[] radii = { m_YellowRadius, m_RedRadius, m_BlueRadius, m_BlackRadius, m_WhiteRadius };
            var vertices = new Vector3[1 + sides * radii.Length];
            for (int ring = 0; ring < radii.Length; ring++)
            {
                for (int side = 0; side < sides; side++)
                {
                    float angle = side * (2 * Mathf.PI / sides);
                    vertices[1 + ring * sides + side] = radii[ring] * new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
                }
            }

            var mesh = new Mesh { name = "Scoring Target Face", vertices = vertices, subMeshCount = radii.Length };
            for (int ring = 0; ring < radii.Length; ring++)
            {
                var triangles = new int[sides * (ring == 0 ? 3 : 6)];
                int start = 1 + ring * sides;
                for (int side = 0, index = 0; side < sides; side++)
                {
                    int outer = start + side;
                    int next = start + (side + 1) % sides;
                    int inner = ring == 0 ? 0 : outer - sides;
                    triangles[index++] = inner;
                    triangles[index++] = next;
                    triangles[index++] = outer;
                    if (ring == 0) continue;
                    triangles[index++] = inner;
                    triangles[index++] = next - sides;
                    triangles[index++] = next;
                }
                mesh.SetTriangles(triangles, ring);
            }
            mesh.RecalculateNormals();
            return mesh;
        }
    }
}
