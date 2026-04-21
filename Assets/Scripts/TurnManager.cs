using UnityEngine;
using System.Linq; 
using System.Collections;

public class TurnManager : MonoBehaviour
{
    public Transform player1;
    public Transform player2;
    public Camera mainCamera;
    public Camera cameraPlayer1;
    public Camera cameraPlayer2;
    private Transform[] casas;

    private int posPlayer1 = 0;
    private int posPlayer2 = 0;

    private bool turnoPlayer1 = true;

    void Awake()
    {
       
        casas = new Transform[transform.childCount];

        for (int i = 0; i < transform.childCount; i++)
        {
            casas[i] = transform.GetChild(i);
        }

        
        casas = casas
            .Where(t => int.TryParse(t.name, out _))
            .OrderBy(t => int.Parse(t.name))
            .ToArray();

        Debug.Log("Total de casas: " + casas.Length);
    }

    void Start()
    {
        AtualizarPosicao();
    }

    void Update()
    {
        Debug.Log("Update a correr");

        if (Input.GetKeyDown(KeyCode.Space))
        {
            Debug.Log("Carregaste espaço");
            JogarTurno();
        }
    }

    void JogarTurno()
    {
        int valorDado = Random.Range(1, 7);
        Debug.Log("Dados: " + valorDado);

        AtivarCameraPlayer();

        StartCoroutine(MoverJogador(valorDado));
    }
    IEnumerator MoverJogador(int passos)
    {
        for (int i = 0; i < passos; i++)
        {
            if (turnoPlayer1)
            {
                posPlayer1 = (posPlayer1 + 1) % casas.Length;
            }
            else
            {
                posPlayer2 = (posPlayer2 + 1) % casas.Length;
            }

            AtualizarPosicao();

            yield return new WaitForSeconds(0.3f); // tempo entre passos
        }

        VoltarCameraPrincipal();
        turnoPlayer1 = !turnoPlayer1;
    }

    void AtualizarPosicao()
    {
        Transform casaAtualP1 = casas[posPlayer1];
        Transform casaAtualP2 = casas[posPlayer2];

        Transform pontoP1 = casaAtualP1.Find("Player1");
        Transform pontoP2 = casaAtualP2.Find("Player2");

        if (pontoP1 != null)
        {
            player1.SetParent(pontoP1);
            player1.localPosition = Vector3.zero;
            player1.localRotation = Quaternion.Euler(0, 0, 90);
        }
        else
        {
            Debug.LogWarning("Casa " + casaAtualP1.name + " não tem Player1");
        }

        if (pontoP2 != null)
        {
            player2.SetParent(pontoP2);
            player2.localPosition = Vector3.zero;
            player2.localRotation = Quaternion.Euler(0, 0, -90);
        }
        else
        {
            Debug.LogWarning("Casa " + casaAtualP2.name + " não tem Player2");
        }
    }

    void AtivarCameraPlayer()
    {
        mainCamera.gameObject.SetActive(false);

        if (turnoPlayer1)
        {
            cameraPlayer1.gameObject.SetActive(true);
        }
        else
        {
            cameraPlayer2.gameObject.SetActive(true);
        }
    }

    void VoltarCameraPrincipal()
    {
        mainCamera.gameObject.SetActive(true);
        cameraPlayer1.gameObject.SetActive(false);
        cameraPlayer2.gameObject.SetActive(false);
    }
}