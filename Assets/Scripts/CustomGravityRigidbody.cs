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
	//水面相关，检测偏移和范围，浮力和阻力大小
	[SerializeField]
	float submergenceOffset = 0.5f;

	[SerializeField, Min(0.1f)]
	float submergenceRange = 1f;

	[SerializeField, Min(0f)]
	float buoyancy = 1f;

	[SerializeField, Range(0f, 10f)]
	float waterDrag = 1f;

	[SerializeField]
	LayerMask waterMask = 0;
	//设置一个浮力起始偏移，以使物体轻重分布
	[SerializeField]
	Vector3 buoyancyOffset = Vector3.zero;

	float submergence;

	Vector3 gravity;

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
		gravity = CustomGravity.GetGravity(body.position);
		if (submergence > 0f)
		{
			//阻力
			float drag =
				Mathf.Max(0f, 1f - waterDrag * submergence * Time.deltaTime);
			body.velocity *= drag;
			body.angularVelocity *= drag;
			//添加浮力，现在将浮力添加到一个点上，使一个面总是在浮力影响下朝上
			body.AddForceAtPosition(
				gravity * -(buoyancy * submergence),
				transform.TransformPoint(buoyancyOffset),
				ForceMode.Acceleration
			);
			submergence = 0f;
		}
		//施加重力,设置为加速度
		body.AddForce(gravity, ForceMode.Acceleration);
	}

	//判断是否进入水面
	void OnTriggerEnter(Collider other)
	{
		if ((waterMask & (1 << other.gameObject.layer)) != 0)
		{
			EvaluateSubmergence();
		}
	}

	void OnTriggerStay(Collider other)
	{
		//刚体不处于睡眠状态就进行浸没程度的评估和操作
		if (!body.IsSleeping() && (waterMask & (1 << other.gameObject.layer)) != 0)
		{
			EvaluateSubmergence();
		}
	}

	void EvaluateSubmergence()
	{
		Vector3 upAxis = -gravity.normalized;
		if (Physics.Raycast(
			body.position + upAxis * submergenceOffset,
			-upAxis, out RaycastHit hit, submergenceRange + 1f,
			waterMask, QueryTriggerInteraction.Collide
		))
		{
			submergence = 1f - hit.distance / submergenceRange;
		}
		else
		{
			submergence = 1f;
		}
	}

}