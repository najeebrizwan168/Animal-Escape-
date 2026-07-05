using UnityEngine;

namespace DogEscape
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class VisionCone : MonoBehaviour
    {
        [Header("Settings")]
        public int meshResolution = 30;
        public Material safeMaterial;
        public Material alertMaterial;

        private CatcherAI catcher;
        private Mesh mesh;
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;

        private void Awake()
        {
            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
            catcher = GetComponentInParent<CatcherAI>();
            
            mesh = new Mesh();
            mesh.name = "Vision Cone Mesh";
            meshFilter.mesh = mesh;
        }

        private void LateUpdate()
        {
            if (catcher == null) return;

            // Switch material based on state
            if (catcher.currentState == CatcherAI.State.Chase)
            {
                if (alertMaterial != null) meshRenderer.sharedMaterial = alertMaterial;
            }
            else
            {
                if (safeMaterial != null) meshRenderer.sharedMaterial = safeMaterial;
            }

            DrawVisionCone();
        }

        private void DrawVisionCone()
        {
            float angleStep = catcher.viewAngle / meshResolution;
            float currentAngle = -catcher.viewAngle / 2f;

            // Mesh arrays
            int vertexCount = meshResolution + 2;
            Vector3[] vertices = new Vector3[vertexCount];
            int[] triangles = new int[meshResolution * 3];

            // Local position origin
            vertices[0] = Vector3.zero;

            for (int i = 0; i <= meshResolution; i++)
            {
                // Calculate direction vector for this ray
                Vector3 dir = Quaternion.Euler(0, currentAngle, 0) * Vector3.forward;
                Vector3 worldDir = transform.parent.TransformDirection(dir);

                // Cast ray to find obstacles
                float distance = catcher.viewDistance;
                RaycastHit hit;
                if (Physics.Raycast(transform.parent.position + Vector3.up * 0.5f, worldDir, out hit, catcher.viewDistance, catcher.obstacleLayer))
                {
                    distance = hit.distance;
                }

                // Convert to local vertex position relative to this transform
                Vector3 localPos = dir * distance;
                // Offset Y slightly to sit just above floor
                localPos.y = 0.05f; 
                vertices[i + 1] = localPos;

                // Build triangle indices
                if (i < meshResolution)
                {
                    triangles[i * 3] = 0;
                    triangles[i * 3 + 1] = i + 1;
                    triangles[i * 3 + 2] = i + 2;
                }

                currentAngle += angleStep;
            }

            // Apply to mesh
            mesh.Clear();
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
        }
    }
}
