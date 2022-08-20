using UnityEngine;

public class MovingSphere : MonoBehaviour
{

	Vector3 playerInput;
	//输入定义空间，指定输入的坐标对象。比如说以摄像机的坐标来判断前后左右
	[SerializeField]
	Transform playerInputSpace = default;
	//正常情况应当绑定不同的动作，这里是球形，暂时先使用两种不同的材质表示攀爬状态和普通状态
	[SerializeField]
	Material normalMaterial = default,
		climbingMaterial = default,
		swimmingMaterial = default;

	[SerializeField, Range(0f, 100f)]
	float maxSpeed = 10f, maxClimbSpeed = 2f, maxSwimSpeed=5f;

	[SerializeField, Range(0f, 100f)]
	float maxAcceleration = 10f, maxAirAcceleration = 1f,
		maxClimbAcceleration = 20f, maxSwimAcceleration=5f;

	[SerializeField, Range(0f, 10f)]
	float jumpHeight = 2f;

	[SerializeField, Range(0, 5)]
	int maxAirJumps = 0;
	//限制爬墙角度
	[SerializeField, Range(90, 180)]
	float maxClimbAngle = 140f;
	//阶梯允许的坡度应当与斜坡不同
	[SerializeField, Range(0, 90)]
	float maxGroundAngle = 25f, maxStairsAngle = 50f;

	[SerializeField, Range(0f, 100f)]
	float maxSnapSpeed = 100f;

	[SerializeField, Min(0f)]
	float probeDistance = 1f;

	[SerializeField]
	float submergenceOffset = 0.5f;

	[SerializeField, Min(0.1f)]
	float submergenceRange = 1f;
	//浮力
	[SerializeField, Min(0f)]
	float buoyancy = 1f;
	//水的阻力
	[SerializeField, Range(0f, 10f)]
	float waterDrag = 1f;
	//游泳需求的深度
	[SerializeField, Range(0.01f, 1f)]
	float swimThreshold = 0.5f;

	[SerializeField]
	LayerMask probeMask = -1, stairsMask = -1, clibmMask = -1, waterMask = 0;
	//保存当前物体刚体，接触物体刚体，上一步接触物体刚体
	Rigidbody body, connectedBody, previousConnectedBody;
	//速度，预期速度，接触速度
	Vector3 velocity, desiredVelocity, connectionVelocity;
	//SteepNormal用于计算垂直平面
	Vector3 contactNormal, steepNormal, climbNormal, lastClimbNormal;
	//接触刚体世界位置
	Vector3 connectionWorldPosition, connectionLocalPosition;

	bool desiredJump, desiresClimbing;

	int groundContactCount, steepContactCount, climbContactCount;

	bool OnGround => groundContactCount > 0;
	//指定向上的轴，用于修改重力方向	
	//指定三个轴，实现不同重力下的正常移动
	//检查Climbing，是否处于攀爬状态，添加一个判断，跳跃后两拍不会视为攀爬
	bool Climbing => climbContactCount > 0 && stepsSinceLastJump>2;
	//使用一个水下部分的值来判断是否处于水下
	bool InWater => submergence > 0f;
	//计算浸入水中的部分的值
	float submergence;
	//判断是否可以游泳
	bool Swimming => submergence >= swimThreshold;

	Vector3 upAxis, rightAxis, forwardAxis;
	bool OnSteep => steepContactCount > 0;

	int jumpPhase;

	int stepsSinceLastGrounded, stepsSinceLastJump;

	float minGroundDotProduct, minStairsDotProduct, minClimbDotProduct;

	MeshRenderer meshRenderer;

	void OnValidate()
	{
		//预先计算比较用的最小点积
		minGroundDotProduct = Mathf.Cos(maxGroundAngle * Mathf.Deg2Rad);
		minStairsDotProduct = Mathf.Cos(maxStairsAngle * Mathf.Deg2Rad);
		minClimbDotProduct = Mathf.Cos(maxClimbAngle * Mathf.Deg2Rad);
	}

	void Awake()
	{
		body = GetComponent<Rigidbody>();
		//应用自定义重力，关闭Unity默认重力
		body.useGravity = false;
		meshRenderer = GetComponent<MeshRenderer>();
		OnValidate();
	}

	void Update()
	{
		playerInput.x = Input.GetAxis("Horizontal");
		playerInput.y = Input.GetAxis("Vertical");
		//添加一个输入来潜水/上浮
		playerInput.z = Swimming ? Input.GetAxis("UpDown") : 0f;
		playerInput = Vector3.ClampMagnitude(playerInput, 1f);
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
		if (Swimming)//游泳状态禁止攀爬和跳跃
		{
			desiresClimbing = false;
		}
		else
		{
			desiredJump |= Input.GetButtonDown("Jump");
			//控制攀爬
			desiresClimbing = Input.GetButton("Climb");

		}

		GetComponent<Renderer>().material.SetColor(
			"_Color", OnGround ? Color.black : Color.white
		);
		//可以攀爬则使用攀爬材质
		//不为攀爬状态则判断是否游泳材质=》游泳状态才更改材质，主要是对应正常的状态改变
		meshRenderer.material = Climbing ? climbingMaterial :
			Swimming ? swimmingMaterial : normalMaterial;

	}

	void FixedUpdate()
	{
		//持续更新重力对应的Up轴

		Vector3 gravity = CustomGravity.GetGravity(body.position, out upAxis);
		UpdateState();
		//添加一个水的阻力系数，按比例降低水中物体的速度
		if (InWater)
		{
			velocity *= 1f - waterDrag * submergence * Time.deltaTime;
		}

		AdjustVelocity();

		if (desiredJump)
		{
			desiredJump = false;
			Jump(gravity);
		}
		//施加重力作为加速度
		//仅在非爬墙状态再施加重力
		if (Climbing) {
			//持续给一个抓向攀爬面的力以适应转角爬行
			velocity -= contactNormal * (maxClimbAcceleration * 0.9f * Time.deltaTime);
		}
		//进入水中之后按照深度给浮力，因此同时会影响浮出水面的比例。毕竟浮力值*浸没值=1就会让球体浮出水面
		else if (InWater)
		{
			velocity +=
				gravity * ((1f - buoyancy * submergence) * Time.deltaTime);
		}
		//当球体处于地面，且速度很低
		else if (OnGround && velocity.sqrMagnitude < 0.01f)
		{
			//重力投影到法线上，而非朝原本重力方向，将球体保持在斜坡上
			//同时球体难以从静止状态脱离，比如卡在缝里
			velocity +=
				contactNormal *
				(Vector3.Dot(gravity, contactNormal) * Time.deltaTime);
		}
		//在地面上希望攀爬，同时应用抓地加速度和重力
		else if (desiresClimbing && OnGround)
		{
			velocity +=
				(gravity - contactNormal * (maxClimbAcceleration * 0.9f)) *
				Time.deltaTime;
		}

		else
		{
			velocity += gravity * Time.deltaTime;
		}

		body.velocity = velocity;
		ClearState();
	}
	//清空数据
	void ClearState()
	{
		groundContactCount = steepContactCount = climbContactCount = 0;

		contactNormal = steepNormal = connectionVelocity = climbNormal = Vector3.zero;
		//下一步长时，当前接触刚体变为上一接触刚体，当前接触刚体清空
		previousConnectedBody = connectedBody;
		connectedBody = null;
		//默认状态不会为游泳状态,修改后以水下浸没值判断游泳状态，直接改值
		submergence = 0f;
	}

	void UpdateState()
	{

		stepsSinceLastGrounded += 1;
		stepsSinceLastJump += 1;
		velocity = body.velocity;
		if (OnGround || SnapToGround() || CheckSteepContacts()||CheckClimbing()||CheckSwimming())
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
	//检测是否在爬坡

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
			out RaycastHit hit, probeDistance, probeMask, QueryTriggerInteraction.Ignore))
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
		//Vector3 xAxis = ProjectDirectionOnPlane(rightAxis, contactNormal);
		//Vector3 zAxis = ProjectDirectionOnPlane(forwardAxis, contactNormal);

		//float acceleration = OnGround ? maxAcceleration : maxAirAcceleration;
		//将原有的仅根据是否处于地面计算的加速度，按照爬墙状态分别计算
		float acceleration, speed;
		Vector3 xAxis, zAxis;
		if (Climbing)
		{
			acceleration = maxClimbAcceleration;
			speed = maxClimbSpeed;
			//爬墙状态使用重力方法输出的重力反方向以及法线叉乘获取副法线作为X轴
			xAxis = Vector3.Cross(contactNormal, upAxis);
			zAxis = upAxis;
		}
		//水中状态将z轴修正为向前轴，也就是只有平面两个轴的移动（和地面一样）
		else if (InWater)
		{
			//水中部分越深，受水面影响越大。此处使用深度/游泳阀值获取影响参数
			//对加速度进行插值
			float swimFactor = Mathf.Min(1f, submergence / swimThreshold);
			acceleration = Mathf.LerpUnclamped(
				maxAcceleration, maxSwimAcceleration, swimFactor
			);
			speed = Mathf.LerpUnclamped(maxSpeed, maxSwimSpeed, swimFactor);
			xAxis = rightAxis;
			zAxis = forwardAxis;
		}
		else
		{//正常状况

			acceleration = OnGround ? maxAcceleration : maxAirAcceleration;
			//在按下攀爬键时限制速度，进入等待攀爬状态，避免爬上顶部会产生的跳起动作
			speed = OnGround && desiresClimbing ? maxClimbSpeed : maxSpeed;
			xAxis = rightAxis;
			zAxis = forwardAxis;
		}
		xAxis = ProjectDirectionOnPlane(xAxis, contactNormal);
		zAxis = ProjectDirectionOnPlane(zAxis, contactNormal);
		//将链接地面的速度添加到物体中，是球体适应移动面的运动
		//球体速度减去链接速度，使用相对速度来获取x和z速度，将原有的绝对速度转为当前连接面的相对速度
		Vector3 relativeVelocity = velocity - connectionVelocity;
		float currentX = Vector3.Dot(relativeVelocity, xAxis);
		float currentZ = Vector3.Dot(relativeVelocity, zAxis);

		float maxSpeedChange = acceleration * Time.deltaTime;

		float newX =
			Mathf.MoveTowards(currentX, playerInput.x * speed, maxSpeedChange);
		float newZ =
			Mathf.MoveTowards(currentZ, playerInput.y * speed, maxSpeedChange);

		velocity += xAxis * (newX - currentX) + zAxis * (newZ - currentZ);
		//同上面一样，添加一个预期移动位置，再应用到速度上
		if (Swimming)
		{
			float currentY = Vector3.Dot(relativeVelocity, upAxis);
			float newY = Mathf.MoveTowards(
				currentY, playerInput.z * speed, maxSpeedChange
			);
			velocity += upAxis * (newY - currentY);
		}

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
		//水中情况下，深度越深跳跃速度越低
			if (InWater)
			{
				jumpSpeed *= Mathf.Max(0f, 1f - submergence / swimThreshold);
			}

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
		//连接到水体就不需要连结信息
		if (Swimming)
		{
			return;
		}
		int layer = collision.gameObject.layer;
		float minDot = GetMinDot(layer);
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
			else
			{
				if (upDot > -0.01f)
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
				//检查是否处于攀爬范围，检查是否有按攀爬键
				if (desiresClimbing && upDot >= minClimbDotProduct && (clibmMask & (1<<layer))!=0)
				{
					climbContactCount += 1;
					climbNormal += normal;
					//记录上次攀爬法线
					lastClimbNormal = normal;
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
	bool CheckClimbing()
	{
		if (Climbing)
		{
			//如果有多个攀爬接触，对法线进行归一化
			//如果有多个接触则视为裂缝状态。使用最后的攀爬法线而非合计值。
			if (climbContactCount > 1)
			{
				climbNormal.Normalize();
				float upDot = Vector3.Dot(upAxis, climbNormal);
				if (upDot >= minGroundDotProduct)
				{
					climbNormal = lastClimbNormal;
				}
			}
			groundContactCount = 1;
			contactNormal = climbNormal;
			return true;
		}
		return false;
	}
	//判断是否进入水面
	void OnTriggerEnter(Collider other)
	{
		if ((waterMask & (1 << other.gameObject.layer)) != 0)
		{
			EvaluateSubmergence(other);
		}
	}

	void OnTriggerStay(Collider other)
	{
		if ((waterMask & (1 << other.gameObject.layer)) != 0)
		{
			EvaluateSubmergence(other);
		}
	}
	void EvaluateSubmergence(Collider other)
	{
		//执行射线检测，偏移点向下到浸入范围。检测是否击中水
		//通过1-击中距离除以范围作为浸入程度
		//增加射线长度，避免因为延迟产生的水下值设为1
		if (Physics.Raycast(
			body.position + upAxis * submergenceOffset,
			-upAxis, out RaycastHit hit, submergenceRange + 1f,
			waterMask, QueryTriggerInteraction.Collide
		))
		{
			submergence = 1f - hit.distance / submergenceRange;
		}
		//彻底浸入之后因为起始点位于水下，不会再击中水面，视为水中（否则因为没有击中点不视为水中）
		else {
			submergence = 1f;
		}
		//连接到水体
		if (Swimming)
		{
			connectedBody = GetComponent<Collider>().attachedRigidbody;
		}
	}
	//检测游泳状态，是则设置地面计数为0，发现为上轴
	bool CheckSwimming()
	{
		if (Swimming)
		{
			groundContactCount = 0;
			contactNormal = upAxis;
			return true;
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