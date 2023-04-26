
//A-Star pathfinding algorithm used to find paths around a hexagon grid map, included a heap implementation to cut down on calculation time

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using System.Diagnostics;

public class PathfindingHandler : MonoBehaviour
{

	public static List<MapHexTile> FindPath(MapHexTile startNode, MapHexTile targetNode, bool mustBeUnblocked = false) {
		
		Heap<MapHexTile> toSearch = new Heap<MapHexTile>(MapGenerator.mg.MaxSize);
		List<MapHexTile> processed = new List<MapHexTile>();//closed
		toSearch.Add(startNode);

		while (toSearch.Count != 0) {
			
				MapHexTile current = toSearch.RemoveFirst();

				processed.Add(current);

				//end point reached
				if (current == targetNode) {
					List<MapHexTile> path = CompletedPath(startNode, targetNode);
					return path;
				}

				foreach (var neighbor in current.neighbors.Where(t =>  !processed.Contains(t))) {//each neighbor not yet processed
					if(neighbor != null && neighbor.transform.parent.gameObject.activeSelf && neighbor.GetComponent<Collider>().enabled){
						
						bool Blocked = false;
						if(neighbor.occupant != null && neighbor != startNode && neighbor != targetNode){
							if(neighbor.occupant.GetComponent<Pickup>() == null){
								Blocked = true;
								
							}else{
								if(mustBeUnblocked == true){
									Blocked = true;
								}
							}
						}
						if(Blocked == false){
							//checks if it is to high to climb
							if(Mathf.Abs(neighbor.transform.position.y - current.transform.position.y) <= MapGenerator.mg.maximumWalkableHeightGap){
								
								var inSearch = toSearch.Contains(neighbor);

								var costToNeighbor = current.g + current.GetDistance(neighbor);

								if (!inSearch || costToNeighbor < neighbor.g) {
									neighbor.g = costToNeighbor;
									neighbor.SetConnection(current);

									if (!inSearch) {
										neighbor.h = neighbor.GetDistance(targetNode);
										toSearch.Add(neighbor);
									}
								}
								
							}
						}
					}
				}

		}
		return null;
	}
	
}
