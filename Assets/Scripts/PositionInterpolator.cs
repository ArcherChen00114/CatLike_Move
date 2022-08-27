using UnityEngine;

public class PositionInterpolator : MonoBehaviour
{
	//绑定一个物体做相对的位移而非使用世界坐标
	[SerializeField]
	Transform relativeTo = default;
	//获取刚体并插值来平滑移动对象
	[SerializeField]
	Rigidbody body = default;

	[SerializeField]
	Vector3 from = default, to = default;

	public void Interpolate(float t)
	{
		Vector3 p;
		if (relativeTo)
		{
			p = Vector3.LerpUnclamped(
				relativeTo.TransformPoint(from), relativeTo.TransformPoint(to), t
			);
		}
		else
		{
			p = Vector3.LerpUnclamped(from, to, t);
		}
		body.MovePosition(p);
	}
}