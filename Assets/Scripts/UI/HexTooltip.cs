using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HexTooltip : MonoBehaviour
{
	[SerializeField]
	GameObject unitInfoPanel = default;

	[SerializeField]
	GameObject cellTooltip = default;

	Text unitName;
	Text unitHitpoints;
	Text unitMovement;
	Text unitAttack;
	Text unitRange;

	private void Start()
	{
		unitInfoPanel.SetActive(false);

		unitName = transform.Find("Unit Info Panel/Unit Name").gameObject.GetComponent<Text>();
		unitHitpoints = transform.Find("Unit Info Panel/Stats/Data/Hitpoints").gameObject.GetComponent<Text>();
		unitMovement = transform.Find("Unit Info Panel/Stats/Data/Movement").gameObject.GetComponent<Text>();
		unitAttack = transform.Find("Unit Info Panel/Stats/Data/Attack").gameObject.GetComponent<Text>();
		unitRange = transform.Find("Unit Info Panel/Stats/Data/Range").gameObject.GetComponent<Text>();
	}

	public void ShowInfoPanel(HexUnit unit)
	{
		if (unit == null)
		{
			unitInfoPanel.SetActive(false);
			return;
		}

		unitName.text = unit.Name;
		unitHitpoints.text = "10";
		unitMovement.text = unit.MovementPoints.ToString();
		unitAttack.text = "10";
		unitRange.text = "1";

		unitInfoPanel.SetActive(true);
	}

	public void ShowTooltip(HexCell cell)
	{
		Debug.Log("Tooltip time!");
	}
}
