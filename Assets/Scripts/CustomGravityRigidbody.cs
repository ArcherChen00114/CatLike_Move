using UnityEngine;

//自定义刚体，需要获取当前刚体
[RequireComponent(typeof(Rigidbody))]
public class CustomGravityRigidbody : MonoBehaviour
{

	Rigidbody body;
	//检测静止时间判断是否进入休眠不计算重力加速度
	float floatDelay;
	//开关控制是否判断休眠
	[SerializeField]
	bool floatToSleep = true;

	void Awake()
	{
		//获取输入刚体
		body = GetComponent<Rigidbody>();
		body.useGravity = false;
	}
	void FixedUpdate()
	{
		GetComponent<Renderer>().material.SetColor(
		   "_Color", body.IsSleeping() ? Color.gray : Color.white
	   );
		//每次FixedUpdate会让刚体不会进入休眠状态，影响优化
		//如果刚体处于睡眠状态，就不动它
		//但是一旦进入睡眠状态就无法打破该状态为其继续施加重力加速度
		//因此添加一个时间差创造施加重力加速度的窗口
		if (floatToSleep)
		{
			if (body.IsSleeping())
			{
				floatDelay = 0f;
				return;
			}
			if (body.velocity.sqrMagnitude < 0.0001f)
			{
				floatDelay += Time.deltaTime;
				if (floatDelay >= 1f)
				{
					return;
				}
			}
			else
			{
				floatDelay = 0f;
			}
		}
		//施加重力,设置为加速度
		body.AddForce(CustomGravity.GetGravity(body.position), ForceMode.Acceleration
			);
	}
}