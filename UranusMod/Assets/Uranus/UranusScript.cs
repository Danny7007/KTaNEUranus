using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using KModkit;

public class UranusScript : MonoBehaviour { //depends on name

    public KMBombInfo Bomb;
    public KMAudio Audio;

    public KMSelectable HideButton;
    public GameObject WholeThing;
    public GameObject Background;
    public GameObject Planet;

    public KMSelectable[] PlanetButtons;

    bool Visible = true;
    bool isAnimating = false;

    //Logging
    static int moduleIdCounter = 1;
    int moduleId;
    private bool moduleSolved;

    int? selected = null;
    int? highlighted = null;

    string[] positions = new string[] { "top", "left", "bottom", "right" };

    void Awake () {
        moduleId = moduleIdCounter++;

        foreach (KMSelectable PlanetButton in PlanetButtons)
        {
            int pos = Array.IndexOf(PlanetButtons, PlanetButton);
            PlanetButton.OnHighlight += delegate () { if (highlighted != null && selected != null) highlighted = pos; };
            PlanetButton.OnInteract += delegate () { selected = pos; return false; };
            PlanetButton.OnInteractEnded += delegate () { Release(pos); };
        }

        HideButton.OnInteract += delegate () { StartCoroutine(HidePlanet()); return false; };

    }

    // Use this for initialization
    void Start ()
    {
        StartCoroutine(PlanetRotation());
    }

    private IEnumerator PlanetRotation()
    {
        var elapsed = 0f;
        while (true)
        {
            Planet.transform.localEulerAngles = new Vector3(elapsed / 2016 * 36000, 0, 0); //depends on time it takes to go around in 1 day
            yield return null;
            elapsed += Time.deltaTime;
        }
    }
    void Release (int pos)
    {
        Debug.Log((selected == null) ? "null" : selected.ToString());
        Debug.Log((highlighted == null) ? "null" : highlighted.ToString());
        if (selected != highlighted && selected != null && highlighted != null )
        {
            int hold = (int)selected;
            int release = (int)highlighted;
            Debug.LogFormat("[Uranus #{0}] You dragged from the {1} node to the {2} node.", moduleId, positions[hold], positions[release]);
        }
        selected = null;
        highlighted = null;

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



}
