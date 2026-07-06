using UnityEngine;

namespace DogEscape
{
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class VisionConeRenderer : MonoBehaviour
    {
        [Header("Settings")]
        public int segments = 30;
        public float heightOffset = 0.05f; // Draw slightly above ground to prevent z-fighting
        public float ConeAngle=1;
        public float ConeSize =2;
        private Mesh filterMesh;
        private MeshRenderer meshRenderer;
        private HunterController hunter;
 
        private void Start()
        {
            hunter = GetComponentInParent<HunterController>();
            
            filterMesh = new Mesh();
            filterMesh.name = "VisionConeMesh";
            GetComponent<MeshFilter>().mesh = filterMesh;

            meshRenderer = GetComponent<MeshRenderer>();
            
            // Create a transparent red material using the URP Lit shader
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (mat.shader.name == "Hidden/InternalErrorShader")
            {
                // Fallback to standard shader if URP Lit isn't found
                mat = new Material(Shader.Find("Standard"));
            }
            
            mat.color = new Color(1f, 0f, 0f, 0.4f); // Transparent Red

            // Configure transparent properties for URP Lit or Standard
            if (mat.HasProperty("_Surface"))
            {
                mat.SetFloat("_Surface", 1.0f); // 1 = Transparent
                mat.SetFloat("_Blend", 0.0f);   // 0 = Alpha
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            }
            else
            {
                // Standard shader transparency settings
                mat.SetFloat("_Mode", 3f); // Transparent mode
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            }

            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            meshRenderer.material = mat;
            
            // Ensure no shadows are cast or received
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
        }

        private void Update()
        {
            // Position flat on the ground relative to the parent
            // Since we place this under the c-hunter root or eyes, localPosition handles translation.
            // If the parent is "eyes" which is elevated, we project the local Y down so it is at floor level.
            if (transform.parent != null)
            {
                // We want the visual cone to be at Y = heightOffset relative to the character's base (root)
                // If parent is elevated, we offset it down relative to parent's height
                float parentY = transform.parent.position.y;
                float localY = heightOffset - parentY;
                transform.localPosition = new Vector3(0f, localY, 0f);

                // If parent is eyes, we cancel out any vertical tilt/roll rotation so the mesh stays flat on XZ plane
                Vector3 parentEuler = transform.parent.eulerAngles;
                transform.rotation = Quaternion.Euler(0f, parentEuler.y, 0f);
            }
            else
            {
                transform.localPosition = new Vector3(0f, heightOffset, 0f);
                transform.localRotation = Quaternion.identity;
            }
            
            // Generate the cone sector based on Hunter settings
            if (hunter != null)
            {
                GenerateConeMesh(ConeAngle, ConeSize);
            }
            else
            {
                GenerateConeMesh(60f, 10f); // default fallback
            }
        }

        [Header("Wall Clipping")]
        public LayerMask wallLayer; // Assign "Wall" layer in Inspector
        public float raycastHeight = 0.5f; // Height from which rays are cast (above ground)

        private void GenerateConeMesh(float angle, float radius)
        {
            int numVertices = segments + 2;
            Vector3[] vertices = new Vector3[numVertices];
            int[] triangles = new int[segments * 3];

            vertices[0] = Vector3.zero; // Origin at hunter's position

            float angleStep = angle / segments;
            float startAngle = -angle / 2f;

            // World origin for raycasts (hunter position at a small height so ray doesn't start underground)
            Vector3 worldOrigin = transform.position + Vector3.up * raycastHeight;

            for (int i = 0; i <= segments; i++)
            {
                float currentAngle = startAngle + i * angleStep;
                float rad = currentAngle * Mathf.Deg2Rad;
                
                // Local direction relative to forward (Z) axis
                Vector3 localDir = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
                
                // Convert local direction to world direction using this object's rotation
                Vector3 worldDir = transform.TransformDirection(localDir);
                worldDir.y = 0f; // Keep ray flat on horizontal plane
                worldDir.Normalize();

                float effectiveRadius = radius;

                // Raycast to check for walls
                RaycastHit hit;
                if (Physics.Raycast(worldOrigin, worldDir, out hit, radius, wallLayer))
                {
                    effectiveRadius = hit.distance;
                }

                vertices[i + 1] = localDir * effectiveRadius;

                if (i < segments)
                {
                    triangles[i * 3] = 0;
                    triangles[i * 3 + 1] = i + 1;
                    triangles[i * 3 + 2] = i + 2;
                }
            }

            filterMesh.Clear();
            filterMesh.vertices = vertices;
            filterMesh.triangles = triangles;
            filterMesh.RecalculateNormals();
            filterMesh.RecalculateBounds();
        }
    }
}
