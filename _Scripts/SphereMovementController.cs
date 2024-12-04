using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System.IO; // For file handling

public class SphereMovementController : MonoBehaviour
{
    public List<GameObject> spheres = new List<GameObject>(); // Reference to dynamically created spheres
    public Camera mainCamera; // Reference to the main camera
    public float speed = 2f; // Movement speed
    public float distanceFromCamera = 5f; // Distance from the camera
    public float visibleTime = 0.7f; // Visible time before the spheres disappear
    public float signal_circleRadius = 2f; // The radius of the circular space for random positions
    public float noise_circleRadius = 3f; // The radius of the circular space for random positions
    public AudioSource audioSource;
    public AudioClip correctSound;
    public AudioClip incorrectSound;
    public TextMeshProUGUI fixationText; // Declare the fixationText as public so it can be assigned in the editor
    public TextMeshProUGUI instructionsText; // Drag your TextMeshPro object here in the Inspector

    private int currentLoop = 0;
    private int direction; // Direction of the signal dots (-1 for left, 1 for right)
    private float selectedCoherence; // Coherence for the current trial
    private List<float[]> trialData = new List<float[]>(); // Store trial direction, coherence, and input

    public int numSpheres = 50; // Number of spheres (total for noise and signal)
    public float dotScale = 0.25f; // Scale of each sphere (dot)

    private List<TrialDef> trialDefs;
    public BlockGenerator blockGen;


    // New variables for instructions
    private string[] instructions = new string[]
    {
        "Welcome to this experiment\nThis is a motion discrimination task.\nYour task is to report the direction (LEFT or RIGHT) of a stimulus.\nPress Space key to continue…",
        "On each trial we will present a patch of visual dots.\nYour task is to indicate the direction of motion on EACH TRIAL by pressing LEFT ARROW for leftward motion or RIGHT ARROW for rightward motion.\nPress Space key to continue…",
        "During each trial, a red cross will appear on the center of the screen.\nPlease FIXATE on the red cross throughout the trial.\nPress Space key to continue...",
        "If the motion stimulus is ambiguous, make a decision as best as you can.\nPlease try to respond as QUICKLY as possible on EACH TRIAL.\n...WE WILL START NOW!...\nWHEN YOU ARE READY: PRESS SPACE KEY TO START THE EXPERIMENT."
    };

    private int currentInstructionIndex = 0; // Keeps track of which instruction is currently being displayed

    void Start()
    {
        // Check if instructionsText is assigned
        if (instructionsText == null)
        {
            Debug.LogError("Please assign instructionsText in the Inspector.");
            return;
        }

        // Start the coroutine to show instructions
        StartCoroutine(ShowInstructionsAndStartExperiment());
    }

    private IEnumerator ShowInstructionsAndStartExperiment()
    {
        // Show each instruction one by one
        while (currentInstructionIndex < instructions.Length)
        {
            instructionsText.text = instructions[currentInstructionIndex]; // Display current instruction
            yield return new WaitForSeconds(1.5f); // Wait for 1.5 seconds to allow reading
            yield return new WaitUntil(() => Input.anyKeyDown); // Wait for any key press
            currentInstructionIndex++;
        }

        // Once all instructions are shown, hide the instructions text
        instructionsText.gameObject.SetActive(false);

        // Wait for a key press before starting the trial
        yield return new WaitUntil(() => Input.anyKeyDown); // Wait for any key press

        // Start the trial
        fixationText.gameObject.SetActive(true); // Show fixation text
        trialDefs = blockGen.GenerateBlock();
        CreateSpheres(); // Create spheres
        StartCoroutine(StartMovementLoop()); // Start the movement loop
    }


    void CreateSpheres()
    {
        GameObject RDK = new GameObject("RDK"); // Parent object for all spheres
        for (int i = 0; i < numSpheres; i++)
        {
            GameObject newSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            newSphere.transform.localScale = Vector3.one * dotScale; // Set scale of the sphere

            // Set the sphere's parent to the RDK object
            newSphere.transform.SetParent(RDK.transform);
            newSphere.SetActive(false); // Initially set spheres to inactive
            spheres.Add(newSphere); // Add the new sphere to the list
        }
    }

    IEnumerator StartMovementLoop()
    {
        while (currentLoop < trialDefs.Count)
        {
            // Logic for setting signal and noise dots
            selectedCoherence = trialDefs[currentLoop].vis_coherence;
            int signalCount = Mathf.RoundToInt(spheres.Count * selectedCoherence);
            int noiseCount = spheres.Count - signalCount;

            direction = trialDefs[currentLoop].vis_direction;

            List<GameObject> signalDots = new List<GameObject>();
            List<GameObject> noiseDots = new List<GameObject>();

            for (int i = 0; i < spheres.Count; i++)
            {
                if (i < signalCount)
                {
                    signalDots.Add(spheres[i]);
                }
                else
                {
                    noiseDots.Add(spheres[i]);
                }
            }

            // Randomize the position of signal spheres
            foreach (var dot in signalDots)
            {
                dot.transform.position = GetRandomPositionInCircle(signal_circleRadius);
                dot.SetActive(true); // Make the sphere visible
            }

            // Randomize the position of noise spheres
            foreach (var dot in noiseDots)
            {
                dot.transform.position = GetRandomPositionInCircle(noise_circleRadius);
                dot.SetActive(true); // Make the sphere visible
            }
            // Start moving signal and noise dots
            StartCoroutine(MoveSignalDots(signalDots, direction));
            StartCoroutine(MoveNoiseDots(noiseDots));

            yield return new WaitForSeconds(visibleTime); // Wait for movement to complete

            // Hide all spheres after the trial
            foreach (var dot in spheres)
            {
                dot.SetActive(false);
            }

            currentLoop++;
            yield return StartCoroutine(WaitForKeyPress());
        }

        // Delete the spheres at the end of the task
        DeleteSpheres();

        // Save trial data to CSV after the experiment
        SaveTrialDataToCSV();
    }

    // Method to delete all created spheres at the end of the task
    void DeleteSpheres()
    {
        foreach (GameObject sphere in spheres)
        {
            Destroy(sphere); // Destroy each sphere object
        }
        spheres.Clear(); // Clear the list of references
        Debug.Log("All spheres deleted");
    }

    // Move the signal dots in a straight line (left or right)
    IEnumerator MoveSignalDots(List<GameObject> signalDots, int direction)
    {
        foreach (var dot in signalDots)
        {
            dot.SetActive(true); // Make the signal dots visible
        }

        float elapsedTime = 0f;
        Vector3 moveDirection = new Vector3(direction, 0, 0); // Move left or right

        while (elapsedTime < visibleTime)
        {
            foreach (var dot in signalDots)
            {
                dot.transform.Translate(moveDirection * speed * Time.deltaTime);
            }

            elapsedTime += Time.deltaTime;
            yield return null;
        }
    }

    // Move the noise dots in random directions
    IEnumerator MoveNoiseDots(List<GameObject> noiseDots)
    {
        foreach (var dot in noiseDots)
        {
            dot.SetActive(true); // Make the noise dots visible
        }

        float elapsedTime = 0f;
        Dictionary<GameObject, Vector3> currentDirections = new Dictionary<GameObject, Vector3>();
        Dictionary<GameObject, float> directionChangeTimers = new Dictionary<GameObject, float>(); // Stores individual change times

        // Assign random initial direction and random direction change interval for each dot
        foreach (var dot in noiseDots)
        {
            currentDirections[dot] = GetRandomDirection();
            directionChangeTimers[dot] = Random.Range(0.2f, 0.7f); // Randomly change direction between 200 ms and 700 ms
        }

        // Move the dots for the visible duration
        while (elapsedTime < visibleTime)
        {
            foreach (var dot in noiseDots)
            {
                dot.transform.Translate(currentDirections[dot] * speed * Time.deltaTime);

                // Check if it's time to change direction based on the dot's individual timer
                directionChangeTimers[dot] -= Time.deltaTime;
                if (directionChangeTimers[dot] <= 0)
                {
                    currentDirections[dot] = GetRandomDirection(); // Assign a new random direction
                    directionChangeTimers[dot] = Random.Range(0.2f, 0.7f); // Reset the change timer to a new random value
                }
            }

            elapsedTime += Time.deltaTime;
            yield return null;
        }
    }

    // Generate a random position within a circle
    Vector3 GetRandomPositionInCircle(float circleRadius)
    {
        Vector2 randomPoint = Random.insideUnitCircle * circleRadius; // Get random point inside a circle of radius
        return mainCamera.transform.position + mainCamera.transform.forward * distanceFromCamera
            + new Vector3(randomPoint.x, randomPoint.y, 0); // Convert the 2D point to 3D space (x, y, 0)
    }

    // Get a random direction vector for the noise dots
    Vector3 GetRandomDirection()
    {
        return new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(-1f, 1f)).normalized;
    }

    IEnumerator WaitForKeyPress()
    {
        bool responseGiven = false;
        int playerDirection = 0;

        while (!responseGiven)
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                playerDirection = -1; // Player pressed left
                responseGiven = true;
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                playerDirection = 1; // Player pressed right
                responseGiven = true;
            }

            yield return null;
        }

        // Store trial data: [direction, selectedCoherence, playerDirection]
        trialData.Add(new float[] { direction, selectedCoherence, playerDirection });
        CheckResponse(playerDirection);
    }

    void CheckResponse(int playerDirection)
    {
        if (playerDirection == direction)
        {
            Debug.Log("Correct!");
            audioSource.PlayOneShot(correctSound);
        }
        else
        {
            Debug.Log("Incorrect!");
            audioSource.PlayOneShot(incorrectSound);
        }
    }

    // Save the trial data to a CSV file
  public int participantNumber; // Manually input participant number
  public int groupNumber;       // Manually input group number
  void SaveTrialDataToCSV()
  {
      int version = 1;
      string fileName = $"vrarVis_{groupNumber}{participantNumber}.csv";
      string filePath = Application.dataPath + "/" + fileName;

      // Check if a file with this name already exists
      while (System.IO.File.Exists(filePath))
      {
          // If it exists, create a new filename with an incremented version number
          fileName = $"vrarVis_{groupNumber}{participantNumber}_v{version}.csv";
          filePath = Application.dataPath + "/" + fileName;
          version++;
      }

      using (StreamWriter writer = new StreamWriter(filePath))
      {
          writer.WriteLine("TrialDirection,Coherence,UserInput"); // Write headers




          foreach (var trial in trialData)
          {
              // Write each trial's data to the CSV file
              writer.WriteLine($"{trial[0]},{trial[1]},{trial[2]}");
          }
      }

      Debug.Log($"Trial data saved to {filePath}");
  }
}
