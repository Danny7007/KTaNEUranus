using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using KModkit;

public class UranusModuleScript : MonoBehaviour
{ 

    public KMBombInfo Bomb;
    public KMAudio Audio;

    public KMSelectable HideButton;
    public GameObject WholeThing, Background, Planet;
    public Transform modTF, parentTF, childTF;

    public KMSelectable[] PlanetButtons;
    public MeshRenderer[] highlightIndicators;
    public Material[] SphereColors;

    bool Visible = true;
    bool isAnimating;
    bool TwitchPlaysActive;

    //Logging
    static int moduleIdCounter = 1;
    int moduleId;
    private bool moduleSolved;

    int? selected = null;
    int? highlighted = null;
    int inputtedDirection = -1;

    string[] positions = new string[] { "top", "left", "bottom", "right" };
    static int[,] directionsTable = new int[,]
    {
        {-1, 5, 4, 3 }, //row represents the first touched node
        {1, -1, 3, 2 }, //col represents the second touched node
        {0, 7, -1, 1 }, //0 is north, directions continue clockwise
        {7, 6, 5, -1 }  //-1 represents not doing anything
    };
    static int[] ValueTable = new int[]
    {
        6,7,5,2,4,5,5,3,1,5,
        5,7,6,8,3,5,2,1,1,4, //We're basically shifting every even column upwards, and then treating left/right movement as jumping over a tile.
        0,5,8,1,4,4,5,5,3,2,
        2,1,4,0,1,4,4,0,1,1,
        8,5,4,8,5,4,7,7,6,2
    };
    string[] directions = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
    string[] directionNames = { "North", "Northeast", "East", "Southeast", "South", "Southwest", "West", "Northwest" };
    Vector3[] axes = { new Vector3(1, 0, 0), new Vector3(1, -1, 0), new Vector3(0, -1, 0), new Vector3(-1, -1, 0), new Vector3(-1, 0, 0), new Vector3(-1, 1, 0), new Vector3(0, 1, 0), new Vector3(1, 1, 0) };
    int currentPosition;
    int targetValue = 0;
    int currentValue = 0;
    int moveCounter = 0;
    int previousCell = -1;
    void Awake()
    {
        moduleId = moduleIdCounter++;

        foreach (KMSelectable PlanetButton in PlanetButtons)
        {
            int pos = Array.IndexOf(PlanetButtons, PlanetButton);
            PlanetButton.OnInteract += delegate () { selected = pos; return false; };
            PlanetButton.OnInteractEnded += delegate () { Release(pos); };
            PlanetButton.OnHighlight += delegate () { highlighted = pos; highlightIndicators[pos].enabled = true; };
            PlanetButton.OnHighlightEnded += delegate () {
                if (pos != selected)
                    highlightIndicators[pos].enabled = false;
                highlighted = null; 
            };
        }
        HideButton.OnInteract += delegate () { StartCoroutine(HidePlanet()); return false; };
    }

    void Start()
    {
        if (UnityEngine.Random.Range(0, 200) == 173) Planet.GetComponent<MeshRenderer>().material = SphereColors[6];
        CalculateTarget();
        currentPosition = UnityEngine.Random.Range(0, 50);
        Debug.LogFormat("[Uranus #{0}] Your starting coordinate is column {1}, row {2}", moduleId, currentPosition % 10 + 1, currentPosition / 10 + 1);
        GetColors();
        StartCoroutine(PlanetRotation());
    }
    void Update()
    {
        if (inputtedDirection != -1)
        {
            parentTF.localEulerAngles = new Vector3(0, 0, 0);
            childTF.SetParent(parentTF); //This code is from kavin. This causes the planet to spin the right way and not gimbal lock.
            parentTF.localRotation *= Quaternion.AngleAxis(90 * Time.deltaTime, axes[inputtedDirection]);
            childTF.SetParent(modTF);
        }
    }
    void CalculateTarget()
    {
        targetValue = Bomb.GetSerialNumberNumbers().Sum();
        if (Bomb.QueryWidgets("volt", "").Count() != 0)
        {
           double voltage = double.Parse(Bomb.QueryWidgets("volt", "")[0].Substring(12).Replace("\"}", ""));
           Debug.LogFormat("[Uranus #{0}] Voltage meter detected with voltage {1}. Bypassing indicator calculations...", moduleId, voltage);
           targetValue += (int)voltage;
        }
        else
            foreach (string indicator in Bomb.GetIndicators())
                switch (indicator)
                {
                    case "SND": case "IND": targetValue += 1; break;
                    case "MSA": case "NSA": targetValue += 2; break;
                    case "FRQ": case "FRK": targetValue += 3; break;
                    case "BOB": case "SIG": targetValue += 4; break;
                    case "CAR": case "CLR": targetValue += 5; break;
                    default               : targetValue += 6; break;
                }
        Debug.LogFormat("[Uranus #{0}] The target value is {1}.", moduleId, targetValue);

    }

    private IEnumerator PlanetRotation()
    {
        while (true)
        {
            Planet.transform.localEulerAngles -= new Vector3(Time.deltaTime / 2016 * 360, 0, 0); //baln code
            yield return null;          
        }
    }
    void Release(int pos)
    {
        if (moduleSolved) return;
        if (Application.isEditor || TwitchPlaysActive) 
            highlighted = pos;
        if (selected != null && highlighted != null && selected != highlighted)
        {
            highlightIndicators[selected.Value].enabled = false;
            inputtedDirection = directionsTable[(int)selected, (int)highlighted];
            Debug.LogFormat("[Uranus #{0}] You dragged from the {1} node to the {2} node. Rolling {3}", moduleId, positions[(int)selected], positions[(int)highlighted], directionNames[inputtedDirection]);
            MoveGrid(inputtedDirection);
        }
        selected = null;
    }

    void MoveGrid(int direction)
    {
        if (!GetAdjacents(currentPosition).ContainsKey(directions[direction]))
        {
            GetComponent<KMBombModule>().HandleStrike();
            Debug.LogFormat("[Uranus #{0}] Tried to move {1} from column {2} row {3}. Strike incurred.", moduleId, directionNames[direction], currentPosition % 10 + 1, currentPosition / 10 + 1);
            inputtedDirection = -1;
        }
        else if (GetAdjacents(currentPosition)[directions[direction]] == previousCell)
        {
            GetComponent<KMBombModule>().HandleStrike();
            Debug.LogFormat("[Uranus #{0}] Tried to move to the previously visited cell. Strike incurred.", moduleId);
            inputtedDirection = -1;
        }
        else
        {
            previousCell = currentPosition;
            currentPosition = GetAdjacents(currentPosition)[directions[direction]];
            currentValue = (moveCounter % 2 == 0) ? currentValue + ValueTable[currentPosition] : currentValue - ValueTable[currentPosition];
            Debug.LogFormat("[Uranus #{0}] Moved {1} to column {2} row {3}. Current value {4} to {5}.",
                              moduleId, directionNames[direction], currentPosition % 10 + 1, currentPosition / 10 + 1, 
                               (moveCounter % 2 == 0) ? "incremented" : "decremented", currentValue);
            moveCounter++;
            if (currentValue != targetValue) Audio.PlaySoundAtTransform("Roll", childTF);
            CheckSolve();
        }
        Debug.Log(GetAdjacents(currentPosition).Join());
    }

    void GetColors()
    { 
        int[] colors = new int[4];
        do
        {
            colors[0] = UnityEngine.Random.Range(0, 6);
            colors[2] = UnityEngine.Random.Range(0, 6);
        } while (colors[0] + colors[2] != currentPosition % 10 + 1);
        do
        {
            colors[1] = UnityEngine.Random.Range(0, 6);
            colors[3] = UnityEngine.Random.Range(0, 6);
        } while (colors[1] - colors[3] != currentPosition / 10 + 1);
        for (int i = 0; i < 4; i++)
        {
            PlanetButtons[i].GetComponent<MeshRenderer>().material = SphereColors[colors[i]];
        }
        Debug.LogFormat("[Uranus #{0}] The sphere colors in NWSE order are as follows: {1}", moduleId, colors.Select(x => SphereColors[x].name).Join(", "));
    }

    void CheckSolve()
    {
        if (currentValue == targetValue)
        {
            moduleSolved = true;
            GetComponent<KMBombModule>().HandlePass();
            Debug.LogFormat("[Uranus #{0}] Target value reached; module solved.", moduleId);
            Audio.PlaySoundAtTransform("BlueSolve", childTF);
            inputtedDirection = -1;
        }
    }

    Dictionary<string, int> GetAdjacents(int input)
    {
        Dictionary<string, int> output = new Dictionary<string, int>();
        //These are the offsets which are consistent for every single cell. I like these cells :)
        if (input > 9) output.Add("N", input - 10);
        if (input % 10 < 8) output.Add("E", input + 2);
        if (input < 40) output.Add("S", input + 10);
        if (input % 10 > 1) output.Add("W", input - 2);
        //These are the offsets which vary depending on whether or not you're on an even column. I do not like these cells very much :(
        if (!(input < 10 && input % 2 == 0 || input % 10 == 9))
            output.Add("NE", (input % 2 == 1) ? input + 1 : input - 9);
        if (!(input > 39 && input % 2 == 1 || input % 10 == 9))
            output.Add("SE", (input % 2 == 1) ? input + 11 : input + 1);
        if (!(input > 39 && input % 2 == 1 || input % 10 == 0))
            output.Add("SW", (input % 2 == 1) ? input + 9 : input - 1);
        if (!(input < 10 && input % 2 == 0 || input % 10 == 0))
            output.Add("NW", (input % 2 == 1) ? input - 1 : input - 11);
        return output;
    }


    private IEnumerator HidePlanet()
    {
        if (isAnimating) yield break;
        isAnimating = true;
        while (Background.transform.localPosition.y < 0.06)
        {
            Background.transform.localPosition += new Vector3(0, 0.0025f, 0);
            Background.transform.localScale += new Vector3(0, 0.005f, 0);
            yield return null;
        }
        Visible = !Visible;
        WholeThing.SetActive(Visible);
        yield return new WaitForSecondsRealtime(0.5f);
        while (Background.transform.localPosition.y > -0.008)
        {
            Background.transform.localPosition -= new Vector3(0, 0.0025f, 0);
            Background.transform.localScale -= new Vector3(0, 0.005f, 0);
            yield return null;
        }
        Debug.LogFormat("<Uranus #{0}> Visible toggled to {1}.", moduleId, Visible);
        yield return null;
        isAnimating = false;
    }
#pragma warning disable 414
    private readonly string TwitchHelpMessage = @"Use !{0} move N NE E SE S SW W NW to move in those directions. Use !{0} hide to press the hide button.";
#pragma warning restore 414

    IEnumerator ProcessTwitchCommand(string input)
    {
        string Command = input.Trim().ToUpperInvariant();
        List<string> parameters = Command.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        int[][] pairs = new int[][] { new int[] { 2, 0 }, new int[] { 2, 3 }, new int[] { 1, 3 }, new int[] { 0, 3 }, new int[] { 0, 2 }, new int[] { 0, 1 }, new int[] { 3, 1 }, new int[] { 3, 0 } };
        if (parameters.First() != "MOVE")
            yield break;
        parameters.Remove("MOVE");
        if (parameters.Any(x => !directions.Contains(x)))
            yield break;
        yield return null;
        foreach (string direction in parameters)
        {
            int[] action = pairs[Array.IndexOf(directions, direction)];
            PlanetButtons[action[0]].OnInteract();
            PlanetButtons[action[1]].OnInteractEnded();
            yield return new WaitForSeconds(0.4f);
        }
    }
    IEnumerator TwitchHandleForcedSolve()
    {
        yield break;
    }
    private List<string> FindPath()
    {
        Queue<QueueItem> q = new Queue<QueueItem>();
        List<Movement> allMoves = new List<Movement>();
        q.Enqueue(new QueueItem(currentPosition, previousCell, currentValue, moveCounter % 2 == 1));
        while (q.Count > 0)
        {
            QueueItem cur = q.Dequeue();
            foreach (KeyValuePair<string, int> movement in GetAdjacents(cur.cell))
            {
                int newCell = movement.Value;
                if (newCell != cur.prevCell)
                {
                    int newScore = cur.subtract ? cur.score - ValueTable[newCell] : cur.score + ValueTable[newCell];
                    q.Enqueue(new QueueItem(newCell, cur.cell, newScore, !cur.subtract));
                    allMoves.Add(new Movement(cur.cell, newCell, movement.Key, newScore));
                }
            }
            if (cur.score == targetValue)
                break;
        }
        Movement lastMove = allMoves.First(x => x.score == targetValue);
        List<string> path = new List<string>();
        while (lastMove.start != currentPosition)
        {
            lastMove = allMoves.First(x => x.end == lastMove.start);
            path += lastMove.direction;
        }
        return path.Reverse().Join("");
    }
}
public struct QueueItem
{
    public int cell, prevCell, score;
    public bool subtract;

    public QueueItem(int cell, int prevCell, int score, bool subtract)
    {
        this.cell = cell;
        this.prevCell = prevCell;
        this.score = score;
        this.subtract = subtract;
    }
}
public class Movement
{
    public int start, end, score;
    public string direction;
    public Movement prev = null;
    public Movement(int start, int end, string direction, int score)
    {
        this.start = start;
        this.end = end;
        this.direction = direction;
        this.score = score;
    }
}
