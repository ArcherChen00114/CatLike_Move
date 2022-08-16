using UnityEngine;

//继承GravitySource，作为另外一种形式
public class GravitySphere : GravitySource
{

	[SerializeField]
	float gravity = 9.81f;
	//重力内圈,衰减范围是小圈，内圈本身是大圈
	[SerializeField, Min(0f)]
	float innerFalloffRadius = 1f, innerRadius = 5f;
	//控制获得最大重力的范围，衰减范围
	[SerializeField, Min(0f)]
	float outerRadius = 10f, outerFalloffRadius = 15f;
	//衰减参数
	float innerFalloffFactor,outerFalloffFactor;

	public override Vector3 GetGravity(Vector3 position)
	{
		//获取距离
		Vector3 vector = transform.position - position;
		float distance = vector.magnitude;
		//小于内圈衰减范围不施加重力，大于外圈衰减不施加重力
		if (distance > outerFalloffRadius || distance < innerFalloffRadius)
		{
			return Vector3.zero;
		}
		float g = gravity / distance;
		//超过最大重力范围随远离距离开始衰减
		//超过衰减距离无重力
		if (distance > outerRadius)
		{
			g *= 1f - (distance - outerRadius) * outerFalloffFactor;
		}
		else if (distance < innerRadius)
		{
			g *= 1f - (innerRadius - distance) * innerFalloffFactor;
		}
		//如果没啥问题g/distance，也就是vector的值，然后再乘回来就是正常的g
		return g * vector;
	}

	void OnDrawGizmos()
	{
		//绘制黄色最大强度范围，蓝色衰减范围
		Vector3 p = transform.position;
		//绘制重力内圈
		if (innerFalloffRadius > 0f && innerFalloffRadius < innerRadius)
		{
			Gizmos.color = Color.cyan;
			Gizmos.DrawWireSphere(p, innerFalloffRadius);
		}
		Gizmos.color = Color.yellow;
		if (innerRadius > 0f && innerRadius < outerRadius)
		{
			Gizmos.DrawWireSphere(p, innerRadius);
		}
		Gizmos.DrawWireSphere(p, outerRadius);
		if (outerFalloffRadius > outerRadius)
		{
			Gizmos.color = Color.cyan;
			Gizmos.DrawWireSphere(p, outerFalloffRadius);
		}
	}
	void Awake()
	{
		OnValidate();
	}

	void OnValidate()
	{
		//确保外圈不会小于内圈，内圈大小不为负数，内圈不小于内圈衰减范围
		innerFalloffRadius = Mathf.Max(innerFalloffRadius, 0f);
		innerRadius = Mathf.Max(innerRadius, innerFalloffRadius);
		outerRadius = Mathf.Max(outerRadius, innerRadius);
		//衰减范围不应小于最大强度范围。
		outerFalloffRadius = Mathf.Max(outerFalloffRadius, outerRadius);
		//以衰减范围设置衰减参数
		innerFalloffFactor = 1f / (innerRadius - innerFalloffRadius);
		outerFalloffFactor = 1f / (outerFalloffRadius - outerRadius);
	}
}