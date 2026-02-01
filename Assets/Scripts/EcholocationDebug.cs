using UnityEngine;

public class EcholocationDebug : MonoBehaviour
{
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            Debug.Log($"[DEBUG] This GameObject position: {transform.position}");
            Debug.Log($"[DEBUG] This GameObject name: {gameObject.name}");
            
            // Check if there's a PlayerMovement component
            PlayerMovement pm = GetComponent<PlayerMovement>();
            if (pm != null)
            {
                Debug.Log("[DEBUG] PlayerMovement found on this GameObject");
            }
            else
            {
                Debug.Log("[DEBUG] PlayerMovement NOT found on this GameObject");
                pm = GetComponentInParent<PlayerMovement>();
                if (pm != null)
                {
                    Debug.Log($"[DEBUG] PlayerMovement found on PARENT: {pm.gameObject.name} at {pm.transform.position}");
                }
            }
            
            // Check global shader values
            Debug.Log($"[DEBUG] Global _Center value: {Shader.GetGlobalVector("_Center")}");
            Debug.Log($"[DEBUG] Global _Radius value: {Shader.GetGlobalFloat("_Radius")}");
        }
    }
}
