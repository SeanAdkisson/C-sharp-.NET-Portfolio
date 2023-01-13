using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using System.Diagnostics;

public class MapGenerator : MonoBehaviour
{
	public bool logGenerationTime;
	public bool wallsAreWater;
	public float mapLength;
	public float mapWidth;
	public int numberOfEnemies;
	public int numberOfPickups;
	public int maxNumberOfRarePickups;
	public int chanceOfRares;
	public int numberOfHills;
	public int numberOfWalls;
	public int wallSize;
	public GameObject fieldPrefab;
	public GameObject wallPrefab;
	public GameObject playerPrefab;
	public List<GameObject> enemyPrefabs;
	public GameObject advancedEnemyPrefab;
	public GameObject pickupPrefab;
	public List<GameObject> rarePickupPrefabs = new List<GameObject>();
	public List<GameObject> advancedPickupPrefabs = new List<GameObject>();
	public GameObject endPointPrefab;
	public BattleController bc;
	public float maximumWalkableHeightGap;//the largest distance between 2 spaces that you can climb up/down
	public static MapGenerator mg;

	public List<GameObject> allTiles = new List<GameObject>();
	Vector3 currentPos;

	public bool finishedGenerating;

	public int MaxSize{
		get {
			return Mathf.RoundToInt(mapLength * mapWidth);
		}
	}

	void Awake(){
		mg = this;
	}
	
	public static Stopwatch watch;
	//average times (starting)
	//level 10: 1200 ms level 20: 3500ms
	//after adding heaps time was the same
	//disabled the lines that change material, there was no change

	void Start(){
		
		//Generate();
		StartCoroutine(StartPremadeMap());
	}

	public void Generate(){
		watch = new Stopwatch();
		watch.Start();
		finishedGenerating = false;

		transform.position = new Vector3(mapWidth/2 -1,0,mapLength/2.4f +1);

		currentPos = Vector3.zero;

		DeleteOld();//destroying is slow and these are not destroyed until the next frame

		for (int i = 0; i < mapWidth; ++i){
			for (int j = 0; j < mapLength; ++j){
				Spawn();
			}
			currentPos += new Vector3 (0.867f,0,0);
			if(i%2 == 0){
				currentPos.z = 0.5f;
			} else{
				currentPos.z = 0;
			}
		}
		if(logGenerationTime)
		UnityEngine.Debug.Log("Tiles finished in: " + MapGenerator.watch.Elapsed.TotalMilliseconds.ToString() + " ms");

		//add walls or gaps
		LayoutWallsAtRandom();
		if(logGenerationTime)
		UnityEngine.Debug.Log("walls finished in: " + MapGenerator.watch.Elapsed.TotalMilliseconds.ToString() + " ms");

		//Set tile neighbors for pathfinding here, BEFORE changing their height and making it more difficult
		foreach(GameObject tile in allTiles){
				if(tile.GetComponentInChildren<MapHexTile>())//checks to see if a wall was placed in this tile
					tile.GetComponentInChildren<MapHexTile>().FindNeighbors();
		}
		if(logGenerationTime)
		UnityEngine.Debug.Log("neighbors set in: " + MapGenerator.watch.Elapsed.TotalMilliseconds.ToString() + " ms");

		//add hills
		for (int i = 0; i < numberOfHills; ++i){
			PlaceRandomHill();
		}

		if(wallsAreWater){
			foreach (GameObject tile in allTiles){
				if(tile.tag == "Wall"){
					Destroy(tile);
				}
			}
		}
		if(logGenerationTime)
		UnityEngine.Debug.Log("hills finished in: " + MapGenerator.watch.Elapsed.TotalMilliseconds.ToString() + " ms");

		//spawn enemies
		for (int i =0; i < numberOfEnemies; i++){
			LayoutObjectsAtRandom(enemyPrefabs[Random.Range(0,enemyPrefabs.Count)], 1, true);
		}
		//LayoutObjectsAtRandom(advancedEnemyPrefab, Mathf.RoundToInt(level/10), true);
		//LayoutObjectsAtRandom(pickupPrefab, numberOfPickups);
		//spawn rare pickups
		for (int i =0; i < maxNumberOfRarePickups; i++){
			if(Random.Range(0,100) <= chanceOfRares){
				LayoutObjectsAtRandom(rarePickupPrefabs[Random.Range(0,rarePickupPrefabs.Count)], 1);
			}
		}

		//spawn end point
		GameObject end = (GameObject)Instantiate(endPointPrefab, allTiles[allTiles.Count-1].transform.position, Quaternion.identity);
		end.transform.SetParent(transform);
		allTiles[allTiles.Count-1].GetComponentInChildren<MapHexTile>().occupant = end;

		//spawn player characters
		/*
		bc.player = (GameObject)Instantiate(playerPrefab,allTiles[0].transform.position,Quaternion.identity);
		bc.player.GetComponent<UnitController>().occupiedSpace = allTiles[0].GetComponentInChildren<MapHexTile>();
		allTiles[0].GetComponentInChildren<MapHexTile>().occupant = bc.player;
		bc.player.transform.SetParent(transform); 
		*/
		if(logGenerationTime)
		UnityEngine.Debug.Log("actors finished in: " + MapGenerator.watch.Elapsed.TotalMilliseconds.ToString() + " ms");

		StartCoroutine(GivePlayerControl());
	}

	IEnumerator GivePlayerControl(){
		yield return new WaitForEndOfFrame();

		MiniMapBehavior.mmb.CenterCameraMap();

		//check if map is completeable
		if(PathfindingHandler.FindMainPath(allTiles[0].GetComponentInChildren<MapHexTile>(), allTiles[allTiles.Count -1].GetComponentInChildren<MapHexTile>()) == null){
			MapGenerator.watch.Stop();
			if(logGenerationTime){
			UnityEngine.Debug.Log("path found in: " + MapGenerator.watch.Elapsed.TotalMilliseconds.ToString() + " ms");
			UnityEngine.Debug.Log("There is no path to the end. Rebuilding");
			}
			Generate();
		}else{
			MapGenerator.watch.Stop();
			if(logGenerationTime)
			UnityEngine.Debug.Log("map built in: " + MapGenerator.watch.Elapsed.TotalMilliseconds.ToString() + " ms");
			finishedGenerating = true;
			//Set enemy AI objectives
			foreach (GameObject enemy in BattleController.bc.allEnemies.Where(t => t != null)){
				enemy.GetComponent<UnitController>().SetFirstObjective();
			}
			yield return new WaitForSeconds(0.5f);

			//bc.player.GetComponent<UnitController>().ShowWalkableSpaces();
		}
	}
	
	IEnumerator StartPremadeMap(){
		yield return new WaitForEndOfFrame();

		MiniMapBehavior.mmb.CenterCameraMap();

		//spawn enemies
		for (int i =0; i < numberOfEnemies; i++){
			LayoutObjectsAtRandom(enemyPrefabs[Random.Range(0,enemyPrefabs.Count)], 1, true);
		}
		//LayoutObjectsAtRandom(advancedEnemyPrefab, Mathf.RoundToInt(level/10), true);
		//LayoutObjectsAtRandom(pickupPrefab, numberOfPickups);
		//spawn rare pickups
		for (int i =0; i < maxNumberOfRarePickups; i++){
			if(Random.Range(0,100) <= chanceOfRares){
				LayoutObjectsAtRandom(rarePickupPrefabs[Random.Range(0,rarePickupPrefabs.Count)], 1);
			}
		}

		yield return new WaitForEndOfFrame();
		
		//Set enemy AI objectives
		foreach (GameObject enemy in BattleController.bc.allEnemies.Where(t => t != null)){
			enemy.GetComponent<UnitController>().SetFirstObjective();
		}
		yield return new WaitForSeconds(0.5f);
}

	void Spawn(){
		GameObject tile = (GameObject)Instantiate(fieldPrefab, currentPos, Quaternion.identity);
		allTiles.Add(tile);
		tile.transform.SetParent(transform);
		currentPos += new Vector3 (0,0,1);
	}

	void LayoutWallsAtRandom ()
	{

		//Instantiate objects until the randomly chosen limit objectCount is reached
		for(int i = 0; i < numberOfWalls; i++)
		{
			//Choose a position for randomPosition by getting a random position from our list of available Vector3s stored in gridPosition
			GameObject randomObject = RandomSpace();

			//Instantiate tileChoice at the position returned by RandomPosition with no change in rotation
			GameObject replacement = (GameObject)Instantiate(wallPrefab, randomObject.transform.position, Quaternion.identity);
			replacement.transform.SetParent(transform);
			allTiles[allTiles.IndexOf(randomObject)] = replacement;
			Destroy(randomObject);

			//replace adjacent space for longer wall
			for(int j=0; j<wallSize -1; j++){
				//throws out of range error
				GameObject adjacentObject = AdjacentSpace(replacement);

				if(adjacentObject){//there is actually something adjacent
					//Instantiate tileChoice at the position returned by RandomPosition with no change in rotation
					replacement = (GameObject)Instantiate(wallPrefab, adjacentObject.transform.position, Quaternion.identity);
					replacement.transform.SetParent(transform);
					allTiles[allTiles.IndexOf(adjacentObject)] = replacement;
					Destroy(adjacentObject);
				}
			}

		}
	}

	//for laying random enemies and items
	void LayoutObjectsAtRandom (GameObject prefab, int amount, bool enemyUnit = false)
	{

		//Instantiate objects until the randomly chosen limit objectCount is reached
		for(int i = 0; i < amount; i++)
		{
			//Choose a position for randomPosition by getting a random position from our list of available Vector3s stored in gridPosition
			GameObject randomObject = RandomSpace();

			if(randomObject.GetComponentInChildren<MapHexTile>() == null){
				i -= 1;
				continue;
			}
			if(randomObject.GetComponentInChildren<MapHexTile>().occupant != null){
				i -= 1;
				continue;
			}

			//Instantiate object at the position returned by RandomSpace with no change in rotation
			GameObject placedObject = (GameObject)Instantiate(prefab, randomObject.transform.position, Quaternion.identity);
			randomObject.GetComponentInChildren<MapHexTile>().occupant = placedObject;
			placedObject.transform.SetParent(transform);

		
			if(enemyUnit){
				placedObject.GetComponent<UnitController>().occupiedSpace = randomObject.GetComponentInChildren<MapHexTile>();
				FindObjectOfType<BattleController>().allEnemies.Add(placedObject);
			}
		}
	}



	GameObject RandomSpace(){
		GameObject chosen = null;
		while(chosen == null){
			chosen = allTiles[Random.Range(1,allTiles.Count - 1)];
		}
		return chosen;
	}

	GameObject AdjacentSpace(GameObject center){
		List<GameObject> allAdjacent = new List<GameObject>();
		foreach (GameObject tile in allTiles){
			if(tile != null){
				if( Vector3.Distance(tile.transform.position, center.transform.position) < 1.5f && tile.tag != "Wall"){
					if(tile != allTiles[allTiles.Count-1] && tile != allTiles[0]){//dont use first and last tile for anything random
						allAdjacent.Add(tile);
					}
				}
			}
		}
		//throws out of range error
		if(allAdjacent.Count > 0){
			return allAdjacent[Random.Range(0,allAdjacent.Count-1)];
		}else{
			return null;
		}
	}

	public void DeleteOld(){
		//foreach (GameObject tile in allTiles){
		//	Destroy(tile);
		//}
		allTiles.Clear();
		//Destroy(GameObject.FindGameObjectWithTag("Player"));
		foreach(Transform child in gameObject.transform){
			Destroy(child.gameObject);
			if(child.GetComponentInChildren<Collider>()){
				child.GetComponentInChildren<Collider>().enabled = false;
			}
		}
	}

	void PlaceRandomHill(){

		Vector3 center = RandomSpace().transform.position;

		float radius = Random.Range(1.0f,5f);

		Collider[] hitColliders = Physics.OverlapSphere(center, radius);
		foreach (Collider hitCollider in hitColliders)
		{
			if(Vector3.Distance(hitCollider.gameObject.transform.position, center) < radius/2){
				hitCollider.gameObject.transform.parent.position += new Vector3(0,0.25f,0);
			} 
			hitCollider.gameObject.transform.parent.position += new Vector3(0,0.25f,0);
		}
	}

	public Text levelText;
	public int level = 1;
	public void IncreaseDifficulty(){
		level += 1;
		mapLength += 1;
		mapWidth += 1;
		numberOfEnemies += 1;
		numberOfPickups += 1;
		numberOfWalls += 1;
		numberOfHills = 5 + Mathf.RoundToInt(level/5);

		if(level >= 10){
			if(rarePickupPrefabs.Contains(advancedPickupPrefabs[0]) == false){
				rarePickupPrefabs.Add( advancedPickupPrefabs[0]);
			}
			maxNumberOfRarePickups = 3 + Mathf.RoundToInt(level/5);
			
		}

		//levelText.text = "Level: " + level;


		//set maximums
		if (mapLength > 46){
			mapLength = 46;
		}
		if (mapWidth > 48){
			mapLength = 48;
		}
		if (numberOfWalls > 40){
			numberOfWalls = 40;
		}
		if (numberOfEnemies > 60){
			numberOfEnemies = 60;
			numberOfPickups = 62;
		}
		if(maxNumberOfRarePickups > 15){
			maxNumberOfRarePickups = 15;
		}
		if(numberOfHills > 14){
			numberOfHills = 14;
		}
	}
}
