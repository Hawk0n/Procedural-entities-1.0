using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class MeshAPIScript : MonoBehaviour
{
    public Material m_material;

    Mesh m_mesh;
    NativeArray<Vector3> m_vertices;

    void Start()
    {
        m_mesh = new Mesh();

        // Set initial vertices
        var tempVertices = new[]
        {
            new Vector3(-1f,0,-1f),
            new Vector3(1f,0,-1f),
            new Vector3(1f,0,1f),
            new Vector3(-1,0,1),
            new Vector3(0,1,0)
        };

        m_vertices = new NativeArray<Vector3>(tempVertices, Allocator.Persistent);
        m_mesh.SetVertexBufferParams(tempVertices.Length, new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32));
        m_mesh.SetVertexBufferData(tempVertices, 0, 0, tempVertices.Length);

        // Set initial indices
        var tempIndices = new[]
        {
            0,4,1, 1,4,2, 2,4,3, 3,4,0
        };
        m_mesh.SetIndexBufferParams(tempIndices.Length, IndexFormat.UInt32);
        m_mesh.SetIndexBufferData(tempIndices, 0, 0, tempIndices.Length);

        // Set initial sub nesh data
        SubMeshDescriptor desc = new SubMeshDescriptor();
        desc.topology = MeshTopology.Triangles;
        desc.firstVertex = 0;
        desc.baseVertex = 0;
        desc.indexStart = 0;
        desc.vertexCount = tempVertices.Length;
        desc.indexCount = tempIndices.Length;
        desc.bounds = new Bounds(Vector3.zero, new Vector3(2, 2, 1));
        m_mesh.SetSubMesh(0, desc);
    }


    void Update()
    {
        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            m_vertices[4] += new Vector3(0, 1, 0);
            m_mesh.SetVertexBufferData(m_vertices, 0, 0, m_vertices.Length);
        }

        if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            m_vertices[4] -= new Vector3(0, 1, 0);
            m_mesh.SetVertexBufferData(m_vertices, 0, 0, m_vertices.Length);
        }


        Graphics.DrawMesh(m_mesh, Matrix4x4.identity, m_material, 0);
    }
}