namespace SushiAndFish.Casino.Original
{
using global::UnityEngine;

public class Trampoline : MonoBehaviour
{
	[SerializeField]
	private Animator animator;

	private void OnCollisionEnter(Collision collision)
	{
		animator.SetTrigger("bounce");
	}
}

}
