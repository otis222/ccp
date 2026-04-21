using UnityEngine;

public class ball : MonoBehaviour
{
   private void OnTriggerEnter(Collider other)
   {
      Destroy(gameObject);
   }
}
