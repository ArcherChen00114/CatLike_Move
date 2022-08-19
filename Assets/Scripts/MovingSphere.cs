using UnityEngine;

public class MovingSphere : MonoBehaviour
{
	//输入定义空间，指定输入的坐标对象。比如说以摄像机的坐标来判断前后左右
	[SerializeField]
	Transform playerInputSpace = default;

	[SerializeField, Range(0f, 100f)]
	float maxSpeed = 10f;

	[SerializeField, Range(0f, 100f)]
	float maxAcceleration = 10f, maxAirAcceleration = 1f;

	[SerializeField, Range(0f, 10f)]
	float jumpHeight = 2f;

	[SerializeField, Range(0, 5)]
	int maxAirJumps = 0;

	//阶梯允许的坡度应当与斜坡不同
	[SerializeField, Range(0, 90)]
	float maxGroundAngle = 25f, maxStairsAngle = 50f;

	[SerializeField, Range(0f, 100f)]
	float maxSnapSpeed = 100f;

	[SerializeField, Min(0f)]
	float probeDistance = 1f;

	[SerializeField]
	LayerMask probeMask = -1, stairsMask = -1;
	//保存当前物体刚体，接触物体刚体，上一步接触物体刚体
	Rigidbody body, connectedBody, previousConnectedBody;
	//速度，预期速度，接触速度
	Vector3 velocity, desiredVelocity, connectionVelocity;

	//SteepNormal用于计算垂直平面
	Vector3 contactNormal, steepNormal;
	//接触刚体世界位置
	Vector3 connectionWorldPosition, connectionLocalPosition;

	bool desiredJump;

	int groundContactCount, steepContactCount;

	bool OnGround => groundContactCount > 0;
	//指定向上的轴，用于修改重力方向	
	//指定三个轴，实现不同重力下的正常移动
	Vector3 upAxis, rightAxis, forwardAxis;
	bool OnSteep => steepContactCount > 0;

	int jumpPhase;

	int stepsSinceLastGrounded, stepsSinceLastJump;

	float minGroundDotProduct, minStairsDotProduct;

	void OnValidate()
	{
		minGroundDotProduct = Mathf.Cos(maxGroundAngle * Mathf.Deg2Rad);
		minStairsDotProduct = Mathf.Cos(maxStairsAngle * Mathf.Deg2Rad);
	}

	void Awake()
	{
		body = GetComponent<Rigidbody>();
		//应用自定义重力，关闭Unity默认重力
		body.useGravity = false;
		OnValidate();
	}

	void Update()
	{
		Vector2 playerInput;
		playerInput.x = Input.GetAxis("Horizontal");
		playerInput.y = Input.GetAxis("Vertical");
		playerInput = Vector2.ClampMagnitude(playerInput, 1f);
		//存在指定输入空间，则从世界空间的方向转为输入空间的方向
		if (playerInputSpace)
		{
			//垂直轨道，也就是观察的角度会影响速度。因为速度相对于输入空间，有一定的y值
			//而移动是在xz空间中的，设置y为0，归一化向量，再缩放速度得出理论速度
			//修正之后 如果存在输入空间，则使用输入空间的前和右来设置除重力之外的轴

			rightAxis = ProjectDirectionOnPlane(playerInputSpace.right, upAxis);
			forwardAxis =
				ProjectDirectionOnPlane(playerInputSpace.forward, upAxis);
		}
		else
		{
			//否则使用向量的正常方向来确定轴
			rightAxis = ProjectDirectionOnPlane(Vector3.right, upAxis);
			forwardAxis = ProjectDirectionOnPlane(Vector3.forward, upAxis);

		}
		desiredVelocity =
			new Vector3(playerInput.x, 0f, playerInput.y) * maxSpeed;

		desiredJump |= Input.GetButtonDown("Jump");

		GetComponent<Renderer>().material.SetColor(
			"_Color", OnGround ? Color.black : Color.white
		);
	}

	void FixedUpdate()
	{
		//持续更新重力对应的Up轴

		Vector3 gravity = CustomGravity.GetGravity(body.position, out upAxis);
		UpdateState();
		AdjustVelocity();

		if (desiredJump)
		{
			desiredJump = false;
			Jump(gravity);
		}
		//施加重力作为加速度
		velocity += gravity * Time.deltaTime;

		body.velocity = velocity;
		ClearState();
	}
	//清空数据
	void ClearState()
	{
		groundContactCount = steepContactCount = 0;

		contactNormal = steepNormal = connectionVelocity = Vector3.zero;
		//下一步长时，当前接触刚体变为上一接触刚体，当前接触刚体清空
		previousConnectedBody = connectedBody;
		connectedBody = null;
	}

	void UpdateState()
	{

		stepsSinceLastGrounded += 1;
		stepsSinceLastJump += 1;
		velocity = body.velocity;
		if (OnGround || SnapToGround() || CheckSteepContacts())
		{
			stepsSinceLastGrounded = 0;
			jumpPhase = 0;
			if (groundContactCount > 1)
			{
				contactNormal.Normalize();
			}
		}
		else
		{
			//修改空中的向上方向为指定的轴
			contactNormal = upAxis;
		}
		//如果有接触刚体设置连结位置为该刚体位置
		if (connectedBody)
		{
			//检测接触刚体质量，以及是否受物理影响。避免被较轻的物体的所受重力带跑
			if (connectedBody.isKinematic || connectedBody.mass >= body.mass)
			{
				UpdateConnectionState();
			}
		}
	}
	void UpdateConnectionState()
	{
		//下面的计算仅在同一链接物体时才有效
		if (connectedBody == previousConnectedBody)
		{
			//当前链接位置减去保存的世界链接位置，除以时间获取速度
			//将本地坐标转回世界坐标再做计算
			Vector3 connectionMovement =
				connectedBody.transform.TransformPoint(connectionLocalPosition) -
				connectionWorldPosition;
			connectionVelocity = connectionMovement / Time.deltaTime;
		}
		//使用球体位置作为世界链接位置，而不是接触的位置
		//将世界坐标系转为球体的本地坐标系
		
		connectionWorldPosition = body.position;
		connectionLocalPosition = connectedBody.transform.InverseTransformPoint(
			connectionWorldPosition
		);

	}
	bool SnapToGround()
	{
		//几个steps不处于地面上
		//并且跳跃2steps不进行速度压制
		if (stepsSinceLastGrounded > 1 || stepsSinceLastJump <= 2)
		{
			return false;
		}
		//检测速度，大于某个速度脱离压制起飞
		float speed = velocity.magnitude;
		if (speed > maxSnapSpeed)
		{
			return false;
		}
		//射线检测下方是否为地面，检测法线是否算作地面，也就是是否超过设定的可移动地面最大角度
		//修改重力之后，下方为指定的重力轴的反方向
		if (!Physics.Raycast(body.position, -upAxis,
			out RaycastHit hit, probeDistance, probeMask))
		{
			return false;
		}
		float upDot = Vector3.Dot(upAxis, steepNormal);
		if (upDot < GetMinDot(hit.collider.gameObject.layer))
		{
			return false;
		}
		//通过以上判定确定处于地面上，但是可能短暂离开地面
		groundContactCount = 1;
		contactNormal = hit.normal;

		//speed获取速度的单位，dot点乘判断速读相对法线的面向
		//速度减去面向，将物体的速度压向地面，归一化之后*速度返回原来的速度

		float dot = Vector3.Dot(velocity, hit.normal);
		//dot如果小于0，重新调整速度反而会减慢向地面收敛的速度，因此仅在>0再收敛
		if (dot > 0f)
		{
			velocity = (velocity - hit.normal * dot).normalized * speed;
		}
		//跟踪连结物体
		connectedBody = hit.rigidbody;
		return true;
	}
	void AdjustVelocity()
	{
		Vector3 xAxis = ProjectDirectionOnPlane(rightAxis, contactNormal);
		Vector3 zAxis = ProjectDirectionOnPlane(forwardAxis, contactNormal);
		//将链接地面的速度添加到物体中，是球体适应移动面的运动
		//球体速度减去链接速度，使用相对速度来获取x和z速度，将原有的绝对速度转为当前连接面的相对速度
		Vector3 relativeVelocity = velocity - connectionVelocity;
		float currentX = Vector3.Dot(relativeVelocity, xAxis);
		float currentZ = Vector3.Dot(relativeVelocity, zAxis);

		float acceleration = OnGround ? maxAcceleration : maxAirAcceleration;
		float maxSpeedChange = acceleration * Time.deltaTime;

		float newX =
			Mathf.MoveTowards(currentX, desiredVelocity.x, maxSpeedChange);
		float newZ =
			Mathf.MoveTowards(currentZ, desiredVelocity.z, maxSpeedChange);

		velocity += xAxis * (newX - currentX) + zAxis * (newZ - currentZ);
	}
	//检查该层是否计算碰撞，每个层获取一个掩码，如果不等于那就不为0
	//不相等返回最小地面点积，如果相等那么返回楼梯点积
	float GetMinDot(int layer)
	{
		return (stairsMask & (1 << layer)) == 0 ?
			minGroundDotProduct : minStairsDotProduct;
	}
	void Jump(Vector3 gravity)
	{
		Vector3 jumpDirection;
		if (OnGround)
		{
			jumpDirection = contactNormal;
		}
		else if (OnSteep)
		{
			jumpDirection = steepNormal;
			jumpPhase = 0;
		}
		else if (maxAirJumps > 0 && jumpPhase <= maxAirJumps)
		{
			if (jumpPhase == 0)
			{
				jumpPhase = 1;
			}
			jumpDirection = contactNormal;
		}
		else
		{
			return;
		}
		stepsSinceLastJump = 0;
		if (stepsSinceLastJump > 1)
		{
			jumpPhase = 0;
		}
		jumpPhase += 1;
		//跳跃速度修改为重力的值而不是重力本身的y（也就是指定的轴）
			float jumpSpeed = Mathf.Sqrt(2f * gravity.magnitude * jumpHeight);
			//修改跳跃方向为重力方向上的向上
			jumpDirection = (jumpDirection + upAxis).normalized;
			float alignedSpeed = Vector3.Dot(velocity, jumpDirection);
			if (alignedSpeed > 0f)
			{
				jumpSpeed = Mathf.Max(jumpSpeed - alignedSpeed, 0f);
			}
			velocity += jumpDirection * jumpSpeed;
		
	}

	void OnCollisionEnter(Collision collision)
	{
		EvaluateCollision(collision);
	}

	void OnCollisionStay(Collision collision)
	{
		EvaluateCollision(collision);
	}

	void EvaluateCollision(Collision collision)
	{
		float minDot = GetMinDot(collision.gameObject.layer);
		for (int i = 0; i < collision.contactCount; i++)
		{
			Vector3 normal = collision.GetContact(i).normal;
			float upDot = Vector3.Dot(upAxis, normal);
			if (upDot >= minDot)
			{
				groundContactCount += 1;
				contactNormal += normal;
				//检测到地面接触，将碰撞的刚体属性分配
				connectedBody = collision.rigidbody;
			}
			//检查有无垂直的接触面，用于防止卡在某些裂缝中
			else if (upDot > -0.01f)
			{
				steepContactCount += 1;
				steepNormal += normal;
				//groundContactCount==0，也就是检测地面法线都没通过，即处于斜坡上。
				//此时优先选择地面，仅在没有地面时才接触斜坡
				if (groundContactCount == 0)
				{
					connectedBody = collision.rigidbody;
				}
			}
		}
	}
	//检查是否有多个大于地面判定的接触面，如果有则返回true视为虚拟地面，允许球体跳跃
	bool CheckSteepContacts()
	{
		if (steepContactCount > 1)
		{
			steepNormal.Normalize();
			float upDot = Vector3.Dot(upAxis, steepNormal);
			if (upDot >= minGroundDotProduct)
			{
				groundContactCount = 1;
				contactNormal = steepNormal;
				return true;
			}
		}
		return false;
	}
	//在平面上投影方向
	//Direction为方向，normal为地面法线
	//指定方向减去法线*两者点乘（也就是方向投影到法线上的长度）
	//减去之后就去除方向在法线上的位移，成为垂直于法线的运动轴
	Vector3 ProjectDirectionOnPlane(Vector3 direction, Vector3 normal)
	{
		//	return vector - contactNormal * Vector3.Dot(vector, contactNormal);
		return (direction - normal * Vector3.Dot(direction, normal)).normalized;
	}
}