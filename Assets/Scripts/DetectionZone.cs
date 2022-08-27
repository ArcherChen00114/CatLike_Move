using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

public class DetectionZone : MonoBehaviour
{

	[SerializeField]
	UnityEvent onFirstEnter = default, onLastExit = default;

	//跟踪碰撞体
	List<Collider> colliders = new List<Collider>();

	void Awake()
	{
		//如果没有任何碰撞体则不激活，会在Colliders的数目为0时为false
		enabled = false;
	}

	//每个步长检测是否生效
	void FixedUpdate()
	{
		//实时检测每一个碰撞体
		for (int i = 0; i < colliders.Count; i++)
		{
			Collider collider = colliders[i];
			if (!collider || !collider.gameObject.activeInHierarchy)
			{
				colliders.RemoveAt(i--);
				if (colliders.Count == 0)
				{
					onLastExit.Invoke();
					enabled = false;
				}
			}
		}
	}
	//停用，销毁时清空并调用退出事件
	void OnDisable()
	{
		//热重载会调用Disable因此在编辑模式下编辑不会调用退出事件
		#if UNITY_EDITOR
				if (enabled && gameObject.activeInHierarchy)
				{
					return;
				}
		#endif

		if (colliders.Count > 0)
		{
			colliders.Clear();
			onLastExit.Invoke();
		}
	}

	void OnTriggerEnter(Collider other)
	{
		if (colliders.Count == 0)
		{
			onFirstEnter.Invoke();
			enabled = false;
		}
		colliders.Add(other);
	}
	//但是不会在停用，禁用，销毁对象时不会调用这玩意
	void OnTriggerExit(Collider other)
	{
		if (colliders.Remove(other) && colliders.Count == 0)
		{
			onLastExit.Invoke();
			enabled = false;
		}
	}

}