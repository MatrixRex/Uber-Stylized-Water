using UnityEngine;

namespace RiverTools
{
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class VertexColorPainter : MonoBehaviour
    {
        [SerializeField] private Mesh m_OriginalMesh;
        [SerializeField] private Mesh m_CopyMesh;

        public Mesh OriginalMesh => m_OriginalMesh;
        public Mesh CopyMesh => m_CopyMesh;

        public void Initialize(Mesh original)
        {
            m_OriginalMesh = original;
            if (original == null) return;

            // Duplicate original mesh to prevent mutating the source asset
            m_CopyMesh = Instantiate(original);
            m_CopyMesh.name = original.name + "_ColorPainted";

            // Initialize vertex colors array to clear black (0,0,0,0) if missing
            Color[] colors = m_CopyMesh.colors;
            if (colors == null || colors.Length != m_CopyMesh.vertexCount)
            {
                colors = new Color[m_CopyMesh.vertexCount];
                for (int i = 0; i < colors.Length; i++)
                {
                    colors[i] = new Color(0f, 0f, 0f, 0f);
                }
                m_CopyMesh.colors = colors;
            }

            GetComponent<MeshFilter>().sharedMesh = m_CopyMesh;
        }

        public void ResetToOriginal()
        {
            if (m_OriginalMesh != null)
            {
                GetComponent<MeshFilter>().sharedMesh = m_OriginalMesh;
            }
            if (m_CopyMesh != null)
            {
                if (Application.isPlaying)
                    Destroy(m_CopyMesh);
                else
                    DestroyImmediate(m_CopyMesh);
            }

            if (Application.isPlaying)
                Destroy(this);
            else
                DestroyImmediate(this, true);
        }

        public void ExportMesh(string savePath)
        {
#if UNITY_EDITOR
            if (m_CopyMesh == null) return;

            // Save copy mesh as an Asset in the project
            UnityEditor.AssetDatabase.CreateAsset(m_CopyMesh, savePath);
            UnityEditor.AssetDatabase.SaveAssets();

            // Update MeshFilter to reference the newly saved Asset mesh
            GetComponent<MeshFilter>().sharedMesh = m_CopyMesh;

            // Clean up the painting helper component since baking is finished
            if (Application.isPlaying)
                Destroy(this);
            else
                DestroyImmediate(this, true);
#endif
        }
    }
}
