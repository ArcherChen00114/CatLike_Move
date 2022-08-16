using UnityEngine;

public class GravityBox : GravitySource
{
	[SerializeField, Min(0f)]
	float innerDistance = 0f, innerFalloffDistance = 0f;

	[SerializeField]
	float gravity = 9.81f;

	[SerializeField]
	Vector3 boundaryDistance = Vector3.one;

	float innerFalloffFactor;
	void Awake()
	{
		OnValidate();
	}

	void OnValidate()
	{
		boundaryDistance = Vector3.Max(boundaryDistance, Vector3.zero);
		//距离的最小值
		float maxInner = Mathf.Min(
			Mathf.Min(boundaryDistance.x, boundaryDistance.y), boundaryDistance.z
		);
		innerDistance = Mathf.Min(innerDistance, maxInner);
		innerFalloffDistance =
			Mathf.Max(Mathf.Min(innerFalloffDistance, maxInner), innerDistance);
		//衰减参数
		innerFalloffFactor = 1f / (innerFalloffDistance - innerDistance);
	}
	float GetGravityComponent(float coordinate, float distance)
	{
		float g = gravity;
		return g;
	}

	void OnDrawGizmos()
	{
		Gizmos.matrix =
			Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
		Vector3 size;
		if (innerFalloffDistance > innerDistance)
		{
			Gizmos.color = Color.cyan;
			size.x = 2f * (boundaryDistance.x - innerFalloffDistance);
			size.y = 2f * (boundaryDistance.y - innerFalloffDistance);
			size.z = 2f * (boundaryDistance.z - innerFalloffDistance);
			Gizmos.DrawWireCube(Vector3.zero, size);
		}
		if (innerDistance > 0f)
		{
			Gizmos.color = Color.yellow;
			size.x = 2f * (boundaryDistance.x - innerDistance);
			size.y = 2f * (boundaryDistance.y - innerDistance);
			size.z = 2f * (boundaryDistance.z - innerDistance);
			Gizmos.DrawWireCube(Vector3.zero, size);
		}
		Gizmos.color = Color.red;
		Gizmos.DrawWireCube(Vector3.zero, 2f * boundaryDistance);
	}
}