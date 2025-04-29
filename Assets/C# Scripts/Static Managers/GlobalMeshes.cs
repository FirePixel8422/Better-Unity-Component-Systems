using UnityEngine;

public static class GlobalMeshes
{
    // Static reference to the cube mesh
    public static Mesh cube;


    // Static constructor to initialize the cube mesh
    static GlobalMeshes()
    {
        cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
    }
}