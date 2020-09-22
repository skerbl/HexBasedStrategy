using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class HexGameUI : MonoBehaviour
{
	[SerializeField]
	HexGrid grid = default;

	[SerializeField]
	HexTooltip tooltip = default;

	private HexCell currentCell;
	private HexUnit selectedUnit;
	private Vector3 lastMousePosition;
	private Vector3 currentMousePosition;
	//private float tooltipTimer = .5f;
	//private bool tooltipActive = false;

	void Update()
	{
		lastMousePosition = currentMousePosition;
		currentMousePosition = Input.mousePosition;

		if (!EventSystem.current.IsPointerOverGameObject())
		{
			if (Input.GetMouseButtonDown(0))
			{
				DoSelection();
			}
			else if (selectedUnit)
			{
				if (Input.GetMouseButtonDown(1))
				{
					DoMove();
				}
				else
				{
					DoPathfinding();
				}
			}
			//else if ((currentMousePosition - lastMousePosition).sqrMagnitude <= 2)
			//{
			//	tooltipTimer -= Time.deltaTime;
			//
			//	if (tooltipTimer < 0 && !tooltipActive)
			//	{
			//		UpdateCurrentCell();
			//		tooltip.ShowTooltip(currentCell);
			//		tooltipActive = true;
			//	}
			//
			//	if (UpdateCurrentCell())
			//	{
			//		tooltipTimer = 0.5f;
			//		tooltipActive = false;
			//	}
			//}
		}
	}

	public void SetEditMode(bool toggle)
	{
		grid.ClearPath();
		selectedUnit = null;
		enabled = !toggle;
		grid.ShowUI(!toggle);

		if (toggle)
		{
			Shader.EnableKeyword("HEX_MAP_EDIT_MODE");
		}
		else
		{
			Shader.DisableKeyword("HEX_MAP_EDIT_MODE");
		}
	}

	bool UpdateCurrentCell()
	{
		HexCell cell = grid.GetCell(Camera.main.ScreenPointToRay(currentMousePosition));
		if (cell != currentCell)
		{
			currentCell = cell;
			return true;
		}
		return false;
	}

	void DoSelection()
	{
		grid.ClearPath();

		UpdateCurrentCell();
		if (currentCell)
		{
			selectedUnit = currentCell.Unit;
			tooltip.ShowInfoPanel(selectedUnit);
		}
	}

	void DoPathfinding()
	{
		if (UpdateCurrentCell())
		{
			if (currentCell && selectedUnit.IsValidDestination(currentCell))
			{
				grid.FindPath(selectedUnit.Location, currentCell, selectedUnit);
			}
			else
			{
				grid.ClearPath();
			}
		}
	}

	void DoMove()
	{
		if (grid.HasPath)
		{
			selectedUnit.Travel(grid.GetPath());
			grid.ClearPath();
			selectedUnit = null;
		}
	}
}