using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class OrbitCamera : MonoBehaviour
{
    //指定摄像头跟随的物体
    [SerializeField]
    Transform focus = default;

    [SerializeField, Range(1f, 20f)]
    float distance = 5f;

    [SerializeField, Min(0f)]
    float focusRadius = 1f;

    [SerializeField, Range(0f, 1f)]
    float focusCentering = 0.5f;

    [SerializeField, Range(1f, 360f)]
    float rotationSpeed = 90f;
    //指定最大反转角度，避免彻底颠倒，使得难以辨认方向
    [SerializeField, Range(-89f, 89f)]
    float minVerticalAngle = -30f, maxVerticalAngle = 60f;
    //设置一个时间，时间到自动对齐到角色正后方
    [SerializeField, Min(0f)]
    float alignDelay = 5f;

    [SerializeField, Range(0f, 90f)]
    float alignSmoothRange = 45f;

    [SerializeField]
    LayerMask obstructionMask = -1;
    //限制上下翻转速度以适应瞬间的重力变化
    [SerializeField, Min(0f)]
    float upAlignmentSpeed = 360f;
    //指定摄像机角度，就如同手柄的左右摇杆，一个控制位置，另外一个控制摄像角度
    Vector2 orbitAngles = new Vector2(45f, 0f);
    //记录最后操作时间
    float lastManualRotationTime;
    //获取重力对齐四元数
    Quaternion gravityAlignment = Quaternion.identity;
    //跟踪轨道旋转
    Quaternion orbitRotation;

    Camera regularCamera;
    //记录上一次的焦点，用于自动调整角度。使用两个焦点计算角色预期的前进方向
    Vector3 focusPoint, previousFocusPoint;
    void Awake()
    {
        //获取摄像机BOX做盒投射
        regularCamera = GetComponent<Camera>();
        focusPoint = focus.position;
        transform.localRotation = orbitRotation = Quaternion.Euler(orbitAngles);
    }
    void OnValidate()
    {
        if (maxVerticalAngle < minVerticalAngle)
        {
            maxVerticalAngle = minVerticalAngle;
        }
    }
    //将输入的垂直轨道旋转角度钳制在规定范围之内
    //水平轨道不限制，但是确保它的数值落在0-360度
    void ConstrainAngles()
    {
        orbitAngles.x =
            Mathf.Clamp(orbitAngles.x, minVerticalAngle, maxVerticalAngle);

        if (orbitAngles.y < 0f)
        {
            orbitAngles.y += 360f;
        }
        else if (orbitAngles.y >= 360f)
        {
            orbitAngles.y -= 360f;
        }
    }
    //获取盒投射所需的BOX的一半延伸
    Vector3 CameraHalfExtends
    {
        get
        {
            Vector3 halfExtends;
            //高度的一半可以通过将相机视场角的一半切线（以弧度为单位）找到
            halfExtends.y =
                regularCamera.nearClipPlane *
                Mathf.Tan(0.5f * Mathf.Deg2Rad * regularCamera.fieldOfView);
            //宽度的一半是由相机的纵横比缩放的。
            halfExtends.x = halfExtends.y * regularCamera.aspect;
            halfExtends.z = 0f;
            return halfExtends;
        }
    }
    //设置控制摄像机输入的方式
    bool ManualRotation()
    {
        //添加输入
        Vector2 input = new Vector2(
            Input.GetAxis("Vertical Camera"),
            Input.GetAxis("Horizontal Camera")
        );
        //添加最小响应值，将对应方向修正到观察角度
        const float e = 0.001f;
        if (input.x < -e || input.x > e || input.y < -e || input.y > e)
        {
            orbitAngles += rotationSpeed * Time.unscaledDeltaTime * input;
            lastManualRotationTime = Time.unscaledTime;
            return true;
        }
        return false;
    }
    //寻找指定方向的水平角度，将2D方向转为角度。
    //使用向量的X来判断是顺时针还是逆时针
    static float GetAngle(Vector2 direction)
    {
        float angle = Mathf.Acos(direction.y) * Mathf.Rad2Deg;
        return direction.x < 0f ? 360f - angle : angle; ;
    }
    bool AutomaticRotation()
    {
        if (Time.unscaledTime - lastManualRotationTime < alignDelay)
        {
            return false;
        }
        //使用当前焦点减去之前焦点获取前进方向向量
        //因为初始以x轴和z轴作为比较对象，因此当重力方向变更，方法失效
        //获取四元数的逆矩阵（？）,获取反重力对齐后的运动增量
        Vector3 alignedDelta =
                Quaternion.Inverse(gravityAlignment) *
                (focusPoint - previousFocusPoint);
        Vector2 movement = new Vector2(alignedDelta.x, alignedDelta.z);
        //如果前进的向量大小小于阈值，则不旋转
        float movementDeltaSqr = movement.sqrMagnitude;
        if (movementDeltaSqr < 0.0001f)
        {
            return false;
        }
        //标准化向量后获取航向角
        float headingAngle = GetAngle(movement / Mathf.Sqrt(movementDeltaSqr));
        //使用旋转速度来限制对齐的速度，设置一个平滑过度角度，
        //当角度插值低于该角度，则缓慢过度
        float deltaAbs = Mathf.Abs(Mathf.DeltaAngle(orbitAngles.y, headingAngle));
        float rotationChange =
            rotationSpeed * Mathf.Min(Time.unscaledDeltaTime, movementDeltaSqr);
        if (deltaAbs < alignSmoothRange)
        {
            rotationChange *= deltaAbs / alignSmoothRange;
        }
        //如果旋转角度超过180度，改变旋转方向
        else if (180f - deltaAbs < alignSmoothRange)
        {
            rotationChange *= (180f - deltaAbs) / alignSmoothRange;
        }
        orbitAngles.y =
            Mathf.MoveTowardsAngle(orbitAngles.y, headingAngle, rotationChange); ;
        return true;
    }
    void UpdateFocusPoint()
    {
        Vector3 targetPoint = focus.position; 
        //限定范围，物体在该范围中时，摄像头不会锁死
        if (focusRadius > 0f)
        {
            //
            float distance = Vector3.Distance(targetPoint, focusPoint);
            float t = 1f;
            if (distance > 0.01f && focusCentering > 0f)
            {
                //unscaledDeltaTime和DeltaTime避免游戏时间对于镜头移动的影响
                //实际上是1-位置的指数次方，用于在一段时间内缓慢移动到指定位置
                //且越接近移动速度越慢
                t = Mathf.Pow(1f - focusCentering, Time.unscaledDeltaTime);
            }
            if (distance > focusRadius)
            {

                t = Mathf.Min(t, focusRadius / distance);
            }
            focusPoint = Vector3.Lerp(targetPoint, focusPoint, t);
        }
        else
        {
            focusPoint = targetPoint;
        }
    }
    //适应重力变化的镜头翻转
    void UpdateGravityAlignment()
    {

        //获取当前重力的向上方向到新的重力的方向旋转，并乘以原四元数获取新的重力四元数
        //获取变换的方向向量
        Vector3 fromUp = gravityAlignment * Vector3.up;
        Vector3 toUp = CustomGravity.GetUpAxis(focusPoint);
        //通过点乘获取变换的cos，顺便钳制值大小
        float dot = Mathf.Clamp(Vector3.Dot(fromUp, toUp), -1f, 1f);
        //将值变换为角度
        float angle = Mathf.Acos(dot) * Mathf.Rad2Deg;
        //限制最大角度为设置的最大速度*时间
        float maxAngle = upAlignmentSpeed * Time.deltaTime;

        Quaternion newAlignment =
            Quaternion.FromToRotation(fromUp, toUp) * gravityAlignment;
        if (angle <= maxAngle)//小于限定角度直接旋转
        {
            gravityAlignment = newAlignment;
        }
        else
        {//大于限定角度，则进行角度插值
            gravityAlignment = Quaternion.SlerpUnclamped(
                gravityAlignment, newAlignment, maxAngle / angle
            );
        }

    }

    // Update is called once per frame
    void Update()
    {
        
    }
    //放置任务重叠，一般物体在Update中更新，相机依赖于物体焦点位置，因此需要LateUpdate
    void LateUpdate()
    {
        UpdateGravityAlignment();
        UpdateFocusPoint();
        //Quaternion lookRotation;
        //定义一个相机的旋转，旋转时限制角度
        if (ManualRotation() || AutomaticRotation())
        {
            ConstrainAngles();
            orbitRotation = Quaternion.Euler(orbitAngles);
        }
        Quaternion lookRotation = gravityAlignment * orbitRotation;
        
        //转为一个Vector3
        Vector3 lookDirection = lookRotation * Vector3.forward;
        //焦点减去观察方向*距离获取观察相机位置，设置位置
        Vector3 lookPosition = focusPoint - lookDirection * distance;
        //检测是否有遮挡，受到遮挡使用命中距离拉近视角避免物体被挡
        //如果盒投射撞到东西，那么最终距离就是命中的距离+近平面距离

        //侦测理想焦点
        Vector3 rectOffset = lookDirection * regularCamera.nearClipPlane;
        Vector3 rectPosition = lookPosition + rectOffset;
        Vector3 castFrom = focus.position;
        Vector3 castLine = rectPosition - castFrom;
        float castDistance = castLine.magnitude;
        Vector3 castDirection = castLine / castDistance;
        if (Physics.BoxCast(
            castFrom, CameraHalfExtends, castDirection, out RaycastHit hit,
            lookRotation, castDistance, obstructionMask, QueryTriggerInteraction.Ignore
        ))
        {
            rectPosition = castFrom + castDirection * hit.distance;
            lookPosition = rectPosition - rectOffset;
        }
        transform.SetPositionAndRotation(lookPosition, lookRotation);
    }
}
