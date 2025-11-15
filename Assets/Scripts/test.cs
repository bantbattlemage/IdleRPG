using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class test : MonoBehaviour
{
	float xAxisInput = 0;
	float yAxisInput = 0;

	private void Start()
	{
		
	}

	private void Update()
	{
		float horizontal = Input.GetAxis("Horizontal");
		float vertical = Input.GetAxis("Vertical");

		if (Input.GetKeyDown(KeyCode.W))
		{
			vertical += 1;
		}

		if (Input.GetKeyDown(KeyCode.S))
		{
			vertical += -1;
		}

		if (Input.GetKeyDown(KeyCode.A))
		{
			horizontal += 1;
		}

		if (Input.GetKeyDown(KeyCode.D))
		{
			horizontal += -1;
		}
	}

	// Start is called before the first frame update
	//void Update()
	//   {
	//      float horizontal = Input.GetAxis("Horizontal");
	//float vertical = Input.GetAxis("Vertical");

	//      if (Input.GetKeyDown(KeyCode.W))
	//      {
	//          vertical += 1;
	//      }

	//      if (Input.GetKeyDown(KeyCode.S)) 
	//      {
	//          vertical += -1;
	//      }

	//if (Input.GetKeyDown(KeyCode.A))
	//{
	//	horizontal += 1;
	//}

	//if (Input.GetKeyDown(KeyCode.D))
	//{
	//	horizontal += -1;
	//}

	//      Debug.Log(horizontal);
	//      Debug.Log(vertical);

	//transform.position += new Vector3(horizontal, 0, vertical);
	//}
}
