using UnityEngine;
using UnityEngine.Events;

public class AutomaticSlider : MonoBehaviour
{

	//是否自动来回移动
	[SerializeField]
	bool autoReverse = false, smoothstep = false;

	public bool AutoReverse
	{
		get => autoReverse;
		set => autoReverse = value;
	}
	//手动控制的切换值
	public bool Reversed { get; set; }
	//Event不会在编辑器中显示，需要为其创建一个事件
	[System.Serializable]
	public class OnValueChangedEvent : UnityEvent<float> { }
	//简单的滚动条
	[SerializeField, Min(0.01f)]
	float duration = 1f;

	//依赖于Events包
	[SerializeField]
	OnValueChangedEvent onValueChanged = default;

	float value;
	//平滑变化，变为3x^2-2x^3
	float SmoothedValue => 3f * value * value - 2f * value * value * value;

	void FixedUpdate()
	{
		float delta = Time.deltaTime / duration;
		if (Reversed)
		{
			value -= delta;
			if (value <= 0f)
			{
				//自动切换有自动的部分
				if (autoReverse)
				{
					//保证数值不溢出，小于0弹回来
					value = Mathf.Min(1f, -value);
					Reversed = false;

				}
				else
				{
					value = 0f;
					enabled = false;
				}
			}
		}
		else
		{
			value += delta;
			if (value >= 1f)
			{
				if (autoReverse)
				{
					value = Mathf.Max(0f, 2f - value);
					Reversed = true;
				}
				else
				{
					value = 1f;
					enabled = false;
				}
			}
		}
		//传递值,根据是否开启平滑应用不同的值
		onValueChanged.Invoke(smoothstep ? SmoothedValue : value);
	}
}