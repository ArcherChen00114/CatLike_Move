using UnityEngine;

//继承GravitySource
public class GravityPlane : GravitySource
{
	//指定重力大小
	[SerializeField]
	float gravity = 9.81f;

	//重力范围
	[SerializeField, Min(0f)]
	float range = 1f;

	//override覆写指定方法
	public override Vector3 GetGravity(Vector3 position)
	{
		//将绝对的向上替换为游戏对象的向上矢量来支持任何方向的平面
		Vector3 up = transform.up;
		//检测距离，大于范围这部分归零
		//distance为角色正上与重力方向的角度差
		float distance = Vector3.Dot(up, position - transform.position);
		if (distance > range)
		{
			return Vector3.zero;
		}

		float g = -gravity;
		if (distance > 0f)
		{
			//与重力的角度差越大，重力越小，为重力翻转做过渡
			g *= 1f - distance / range;
		}
		return g * up;
	}
	//调用Gizmos绘制重力面
	void OnDrawGizmos()
	{
		Vector3 scale = transform.localScale;
		scale.y = range;
		Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, scale);

		Vector3 size = new Vector3(1f, 0f, 1f);
		Gizmos.color = Color.yellow;
		Gizmos.DrawWireCube(Vector3.zero, size);
        if (range > 0f) {
			Gizmos.color = Color.cyan;
			Gizmos.DrawWireCube(Vector3.up, size);
		}
	}
}