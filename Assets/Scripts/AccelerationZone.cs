using UnityEngine;

public class AccelerationZone : MonoBehaviour
{

	[SerializeField, Min(0f)]
	float acceleration = 10f, speed = 10f;

	//在物体进入时储法
	void OnTriggerEnter(Collider other)
	{
		//获取碰撞物体的刚体，如果有刚体，则调用下面的Accelerate方法
		Rigidbody body = other.attachedRigidbody;
		if (body)
		{
			Accelerate(body);
		}
	}
	//保持在范围中可以获得加速度
	void OnTriggerStay(Collider other)
	{
		Rigidbody body = other.attachedRigidbody;
		if (body)
		{
			Accelerate(body);
		}
	}

	void Accelerate(Rigidbody body) {
		//获取刚体速度，如果已经超过预设速度，则跳过。未达到预设速度设为预设速度
		//转为物体的局部空间的速度方向
		Vector3 velocity = transform.InverseTransformDirection(body.velocity);
		if (velocity.y >= speed)
		{
			return;
		}
		//如果效果持续时间较长，使用加速度而非直接的速度变化实现
		if (acceleration > 0f)
		{
			velocity.y = Mathf.MoveTowards(
				velocity.y, speed, acceleration * Time.deltaTime
			);
		}
		else
		{
			velocity.y = speed;
		}

		body.velocity = transform.TransformDirection(velocity);
		if (body.TryGetComponent(out MovingSphere sphere))
		{
			sphere.PreventSnapToGround();
		}
	}

	

}