using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class CustomGravity
{

	//支持多个重力源，需要上面System.Collections.Generic的支持
	static List<GravitySource> sources = new List<GravitySource>();
	//遍历重力源来累积重力
	//获取重力向量
	public static Vector3 GetGravity(Vector3 position)
	{
		Vector3 g = Vector3.zero;
		for (int i = 0; i < sources.Count; i++)
		{
			g += sources[i].GetGravity(position);
		}
		return g;
	}

	//获取重力向量的相反向量，也就是垂直的Y轴方向
	public static Vector3 GetUpAxis(Vector3 position)
	{
		Vector3 g = Vector3.zero;
		for (int i = 0; i < sources.Count; i++)
		{
			g += sources[i].GetGravity(position);
		}
		return -g.normalized;
	}
	//一次性把上面两个的活儿都干了，通过Out关键字将UpAxis一同输出
	//out关键字告诉我们该方法负责正确设置参数
	//并替换其先前的值。不为其分配值将会产生编译器错误。
	public static Vector3 GetGravity(Vector3 position, out Vector3 upAxis)
	{
		Vector3 g = Vector3.zero;
		for (int i = 0; i < sources.Count; i++)
		{
			g += sources[i].GetGravity(position);
		}
		upAxis = -g.normalized;
		return g;
	}
	public static void Register(GravitySource source)
	{
		//当然不能添加同个重力源
		Debug.Assert(
			   !sources.Contains(source),
			   "Duplicate registration of gravity source!", source
		   );
		sources.Add(source);
	}

	public static void Unregister(GravitySource source)
	{
		//当然也不可能删除不存在的重力源
		Debug.Assert(
			   sources.Contains(source),
			   "Unregistration of unknown gravity source!", source
		   );
		sources.Remove(source);
	}

}