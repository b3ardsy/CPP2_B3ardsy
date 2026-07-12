using UnityEngine;
using System.Collections;

public class SPNECloudsControl : MonoBehaviour {

	//Range for min/max values of variable
	[Range(-100f, 100f)]
	public float cloudsMoveSpeed_x, cloudsMoveSpeed_z;

	// Clouds Movement
	void Update () {
		gameObject.transform.Translate (cloudsMoveSpeed_x * Time.deltaTime, 0f, cloudsMoveSpeed_z * Time.deltaTime);
	}
}
