using System.Collections.Generic;
using UnityEngine;

namespace RiverTools
{
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class VertexColorPainter : MonoBehaviour
    {
        [HideInInspector] [SerializeField] private Mesh m_OriginalMesh;
        [HideInInspector] [SerializeField] private Mesh m_CopyMesh;

        public Mesh OriginalMesh => m_OriginalMesh;
        public Mesh CopyMesh => m_CopyMesh;

        public void Initialize(Mesh original)
        {
            m_OriginalMesh = original;
            if (original == null) return;

            // Duplicate original mesh to prevent mutating the source asset
            m_CopyMesh = Instantiate(original);
            m_CopyMesh.name = original.name + "_ColorPainted";

            // Initialize vertex colors array to clear black with 1.0 alpha (no overlay) if missing
            Color[] colors = m_CopyMesh.colors;
            if (colors == null || colors.Length != m_CopyMesh.vertexCount)
            {
                colors = new Color[m_CopyMesh.vertexCount];
                for (int i = 0; i < colors.Length; i++)
                {
                    colors[i] = new Color(0f, 0f, 0f, 1f); // Alpha = 1.0 (no overlay)
                }
                m_CopyMesh.colors = colors;
            }

            // Initialize UV2 (channel 1) as Vector4 list if missing or incorrect length
            List<Vector4> uv2 = new List<Vector4>();
            m_CopyMesh.GetUVs(1, uv2);
            if (uv2 == null || uv2.Count != m_CopyMesh.vertexCount)
            {
                uv2 = new List<Vector4>(m_CopyMesh.vertexCount);
                for (int i = 0; i < m_CopyMesh.vertexCount; i++)
                {
                    uv2.Add(new Vector4(1f, 1f, 1f, 0f)); // W component is 0 (no reduction)
                }
                m_CopyMesh.SetUVs(1, uv2);
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
