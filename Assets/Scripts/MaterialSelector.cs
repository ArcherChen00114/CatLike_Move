using UnityEngine;

public class MaterialSelector : MonoBehaviour
{
	//材质数组
	[SerializeField]
	Material[] materials = default;
	//渲染器
	[SerializeField]
	MeshRenderer meshRenderer = default;
	//指定编号来选择材质
	public void Select(int index)
	{
		if (
			meshRenderer && materials != null &&
			index >= 0 && index < materials.Length
		)
		{
			meshRenderer.material = materials[index];
		}
	}
}