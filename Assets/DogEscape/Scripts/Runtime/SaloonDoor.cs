using UnityEngine;

public class SaloonDoor : MonoBehaviour
{
    // Ab yahan kisi AudioSource ki zaroorat nahi, sab kuch saaf!
    public bool bricksWallsStand=true;
    private void OnTriggerEnter(Collider other)
    {
        // Agar Player ya Hunter mein se koi bhi collide kare
        if (other.CompareTag("Player") || other.CompareTag("Hunter"))
        {
            // Central manager ko bolna ke is darwaze ki position par sound play kare
            if (UniversalSoundManager.Instance != null && gameObject.tag!="blockDoor")
            {
                UniversalSoundManager.Instance.PlaySaloonDoorSound(transform.position);
            }
            else if(UniversalSoundManager.Instance != null && gameObject.tag=="blockDoor" && bricksWallsStand==true)
            {
                UniversalSoundManager.Instance.PlayBrickWall(transform.position);
                bricksWallsStand=false;
                
            }
            else if(UniversalSoundManager.Instance != null && gameObject.tag=="blockDoor" && bricksWallsStand==false)
            {
                UniversalSoundManager.Instance.PlayBrickWallRemove(transform.position);
                
                
            }
        }
    }
}