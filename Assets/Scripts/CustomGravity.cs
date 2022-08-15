using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class CustomGravity
{
	//获取重力向量
	public static Vector3 GetGravity(Vector3 position)
	{
		return position.normalized * Physics.gravity.y;
	}
	//获取重力向量的相反向量，也就是垂直的Y轴方向
	public static Vector3 GetUpAxis(Vector3 position)
	{
		//指向地点，形成以某点为中心圆形重力场
		Vector3 up = position.normalized;
		return Physics.gravity.y < 0f ? up : -up;
	}
	//一次性把上面两个的活儿都干了，通过Out关键字将UpAxis一同输出
	//out关键字告诉我们该方法负责正确设置参数
	//并替换其先前的值。不为其分配值将会产生编译器错误。
	public static Vector3 GetGravity(Vector3 position, out Vector3 upAxis)
	{
		Vector3 up = position.normalized;
		upAxis = Physics.gravity.y < 0f ? up : -up;
		return up * Physics.gravity.y;
	}


}