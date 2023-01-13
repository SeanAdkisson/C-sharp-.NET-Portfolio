using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using System.Diagnostics;

public class PathfindingHandler : MonoBehaviour
{

	public static Color PathColor = new Color(1f, 0.33f, 0.0f);
	public static  Color OpenColor = new Color(.4f, .6f, .4f);
	public static  Color ClosedColor = new Color(0.35f, 0.4f, 0.5f);

	public static List<MapHexTile> FindPath(MapHexTile startNode, MapHexTile targetNode, bool mustBeUnblocked = false) {
		//List<MapHexTile> toSearch = new List<MapHexTile>() { startNode };//open
		Heap<MapHexTile> toSearch = new Heap<MapHexTile>(MapGenerator.mg.MaxSize);
		List<MapHexTile> processed = new List<MapHexTile>();//closed
		toSearch.Add(startNode);

		while (toSearch.Count != 0) {
			
				//MapHexTile current = toSearch[0];
				//foreach (MapHexTile t in toSearch) 
				//	if (t.f < current.f || t.f == current.f && t.h < current.h) current = t;
				//
				//toSearch.Remove(current);
				MapHexTile current = toSearch.RemoveFirst();

				processed.Add(current);
		

					//end point reached
					if (current == targetNode) {
						List<MapHexTile> path = CompletedPath(startNode, targetNode);
						return path;
					}

						//occupant works for the player, but not the enemies, since the players space is occupied any path to the players reads blocked
				foreach (var neighbor in current.neighbors.Where(t => /*t.occupant == null &&*/ !processed.Contains(t))) {//each neighbor not yet processed
					if(neighbor != null && neighbor.transform.parent.gameObject.activeSelf && neighbor.GetComponent<Collider>().enabled){
						//works for player, tons of lag
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
							if(Mathf.Abs(neighbor.transform.position.y - current.transform.position.y) <= MapGenerator.mg.maximumWalkableHeightGap){//check if it is to high to climb
								
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

	static List<MapHexTile> CompletedPath(MapHexTile startNode, MapHexTile finalSpace, bool mainPath = false){
		MapHexTile currentPathTile = finalSpace;
		List<MapHexTile> path = new List<MapHexTile>();
		int count = 100;
		//build path list (comes out backwards with 0 being the end)
		while (currentPathTile != startNode) {
			path.Add(currentPathTile);
			currentPathTile = currentPathTile.connection;
			count--;
			string generationError =  mainPath == true ? "Map is being rebuilt" : "";
			if (count < 0) UnityEngine.Debug.Log("path not found." + generationError);//throw new Exception();
			//Debug.Log("exception thrown during pathfinding");
		}

		if(mainPath){
			foreach (var tile in path) tile.gameObject.GetComponent<Renderer>().material.color = PathColor;
			startNode.gameObject.GetComponent<Renderer>().material.color = PathColor;
		}
		return path;
	}

	//find the main path through the level (also changes the floor color to create a path on the floor, used in map generation)
	public static List<MapHexTile> FindMainPath(MapHexTile startNode, MapHexTile targetNode) {
		//List<MapHexTile> toSearch = new List<MapHexTile>() { startNode };//open
		Heap<MapHexTile> toSearch = new Heap<MapHexTile>(MapGenerator.mg.MaxSize);
		List<MapHexTile> processed = new List<MapHexTile>();
		toSearch.Add(startNode);

		while (toSearch.Count != 0) {
			
				//MapHexTile current = toSearch[0];
				//foreach (MapHexTile t in toSearch) 
				//	if (t.f < current.f || t.f == current.f && t.h < current.h) current = t;
				//
				//toSearch.Remove(current);
				MapHexTile current = toSearch.RemoveFirst();

				processed.Add(current);

				current.gameObject.GetComponent<Renderer>().material.color = ClosedColor;

					//end point reached
					if (current == targetNode) {
						List<MapHexTile> path = CompletedPath(startNode, targetNode, true);
					
						return path;
					}


				foreach (var neighbor in current.neighbors.Where(t => /*t.occupant == null &&*/ !processed.Contains(t))) {//each neighbor not yet processed
					if(neighbor != null){
						if(Mathf.Abs(neighbor.transform.position.y - current.transform.position.y) <= MapGenerator.mg.maximumWalkableHeightGap){//check if it is to high to climb
							var inSearch = toSearch.Contains(neighbor);

							var costToNeighbor = current.g + current.GetDistance(neighbor);

							if (!inSearch || costToNeighbor < neighbor.g) {
								neighbor.g = costToNeighbor;
								neighbor.SetConnection(current);

								if (!inSearch) {
									neighbor.h = neighbor.GetDistance(targetNode);
									toSearch.Add(neighbor);
									neighbor.GetComponent<Renderer>().material.color = OpenColor;
								}
							}
						}
					}
				}

		}
		return null;
	}
}