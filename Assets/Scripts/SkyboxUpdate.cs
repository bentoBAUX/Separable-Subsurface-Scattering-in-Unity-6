using UnityEngine;

[ExecuteAlways]
public class SkyboxUpdate : MonoBehaviour
{
    public Material skyboxMaterial;
    void Update()
    {
        DynamicGI.UpdateEnvironment();
    }
}