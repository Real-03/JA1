using UnityEngine;
using System.Linq;
using System.Collections;
using TMPro;
using UnityEngine.UI;

public class TurnManager : MonoBehaviour
{
    [Header("Jogadores")]
    public Transform player1;
    public Transform player2;

    [Header("Câmeras")]
    public Camera mainCamera;
    public Camera cameraPlayer1;
    public Camera cameraPlayer2;

    [Header("UI - Dado")]
    public GameObject painelDado;
    public TMP_Text textDado;

    [Header("UI - Casa")]
    public GameObject painelCasa;
    public TMP_Text textCasa;
    public Button botaoComprar;
    public Button botaoRecusar;

    [Header("UI - Dinheiro")]
    public TMP_Text textDinheiroP1;
    public TMP_Text textDinheiroP2;

    [Header("Configurações")]
    public int dinheiroInicial = 1500;
    public int dinheiroVoltaCompleta = 200;
    public int precoCasa = 300;
    public int taxaCasa = 150;

    // Dados internos
    private Transform[] casas;
    private int posPlayer1 = 0;
    private int posPlayer2 = 0;
    private bool turnoPlayer1 = true;
    private bool aguardandoInput = false;

    // Dinheiro
    private int dinheiroP1;
    private int dinheiroP2;

    // Proprietários das casas: índice da casa é 0 = ninguém, 1 = P1, 2 = P2
    private int[] proprietarios;

    void Awake()
    {
        casas = new Transform[transform.childCount];
        for (int i = 0; i < transform.childCount; i++)
            casas[i] = transform.GetChild(i);

        casas = casas
            .Where(t => int.TryParse(t.name, out _))
            .OrderBy(t => int.Parse(t.name))
            .ToArray();

        proprietarios = new int[casas.Length]; // todos começam a 0
    }

    void Start()
    {
        dinheiroP1 = dinheiroInicial;
        dinheiroP2 = dinheiroInicial;

        painelDado.SetActive(false);
        painelCasa.SetActive(false);

        AtualizarPosicao();
        AtualizarUIDinheiro();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space) && !aguardandoInput)
        {
            JogarTurno();
        }
    }

    void JogarTurno()
    {
        int valorDado = Random.Range(1, 7);
        AtivarCameraPlayer();
        StartCoroutine(FluxoTurno(valorDado));
    }

    IEnumerator FluxoTurno(int valorDado)
    {
        aguardandoInput = true;

        // 1. Mostrar número do dado
        painelDado.SetActive(true);
        textDado.text = "Saiu o número: " + valorDado;
        yield return new WaitForSeconds(2f);
        painelDado.SetActive(false);

        // 2. Mover jogador
        int posAntes = turnoPlayer1 ? posPlayer1 : posPlayer2;
        yield return StartCoroutine(MoverJogador(valorDado));
        int posDepois = turnoPlayer1 ? posPlayer1 : posPlayer2;

        // 3. Verificar volta completa
        if (posDepois < posAntes || (posAntes == 0 && posDepois == valorDado - 1))
        {
            // deu a volta (posição voltou ao início)
        }
        // Forma mais simples: se a posição nova < posição antes = deu a volta
        if (posDepois <= posAntes && valorDado > 0)
        {
            if (turnoPlayer1)
            {
                dinheiroP1 += dinheiroVoltaCompleta;
                Debug.Log("P1 completou volta! +" + dinheiroVoltaCompleta);
            }
            else
            {
                dinheiroP2 += dinheiroVoltaCompleta;
                Debug.Log("P2 completou volta! +" + dinheiroVoltaCompleta);
            }
            AtualizarUIDinheiro();
        }

        // 4. Verificar casa atual
        yield return StartCoroutine(VerificarCasa());

        // 5. Terminar turno
        VoltarCameraPrincipal();
        turnoPlayer1 = !turnoPlayer1;
        aguardandoInput = false;
    }

    IEnumerator MoverJogador(int passos)
    {
        for (int i = 0; i < passos; i++)
        {
            if (turnoPlayer1)
                posPlayer1 = (posPlayer1 + 1) % casas.Length;
            else
                posPlayer2 = (posPlayer2 + 1) % casas.Length;

            AtualizarPosicao();
            yield return new WaitForSeconds(0.3f);
        }
    }

    IEnumerator VerificarCasa()
    {
        int posAtual = turnoPlayer1 ? posPlayer1 : posPlayer2;
        int jogadorAtual = turnoPlayer1 ? 1 : 2;
        int jogadorOponente = turnoPlayer1 ? 2 : 1;
        int proprietario = proprietarios[posAtual];

        // Casa pertence ao oponente = pagar taxa
        if (proprietario == jogadorOponente)
        {
            textCasa.text = "Esta casa pertence ao Jogador " + jogadorOponente +
                            "!\nTens de pagar uma taxa de " + taxaCasa + "€";
            botaoComprar.gameObject.SetActive(false);
            botaoRecusar.GetComponentInChildren<TMP_Text>().text = "OK";
            painelCasa.SetActive(true);

            bool esperando = true;
            botaoRecusar.onClick.RemoveAllListeners();
            botaoRecusar.onClick.AddListener(() => esperando = false);

            yield return new WaitUntil(() => !esperando);
            painelCasa.SetActive(false);

            // Cobrar taxa
            if (turnoPlayer1)
            {
                dinheiroP1 -= taxaCasa;
                dinheiroP2 += taxaCasa;
            }
            else
            {
                dinheiroP2 -= taxaCasa;
                dinheiroP1 += taxaCasa;
            }
            AtualizarUIDinheiro();
        }
        // Casa é neutra = pode comprar
        else if (proprietario == 0)
        {
            textCasa.text = "Casa disponível!\nPreço: " + precoCasa + "€\nQueres comprar?";
            botaoComprar.gameObject.SetActive(true);
            botaoRecusar.GetComponentInChildren<TMP_Text>().text = "Recusar";
            painelCasa.SetActive(true);

            bool comprou = false;
            bool decidiu = false;

            botaoComprar.onClick.RemoveAllListeners();
            botaoRecusar.onClick.RemoveAllListeners();

            botaoComprar.onClick.AddListener(() => { comprou = true; decidiu = true; });
            botaoRecusar.onClick.AddListener(() => { decidiu = true; });

            yield return new WaitUntil(() => decidiu);
            painelCasa.SetActive(false);

            if (comprou)
            {
                int dinheiro = turnoPlayer1 ? dinheiroP1 : dinheiroP2;
                if (dinheiro >= precoCasa)
                {
                    proprietarios[posAtual] = jogadorAtual;
                    if (turnoPlayer1) dinheiroP1 -= precoCasa;
                    else dinheiroP2 -= precoCasa;
                    AtualizarUIDinheiro();
                    Debug.Log("Jogador " + jogadorAtual + " comprou a casa " + posAtual);
                }
                else
                {
                    Debug.Log("Dinheiro insuficiente!");
                }
            }
        }
        // Casa já é do próprio jogador não faz nada
    }

    void AtualizarUIDinheiro()
    {
        textDinheiroP1.text = "P1: " + dinheiroP1 + "€";
        textDinheiroP2.text = "P2: " + dinheiroP2 + "€";
    }

    void AtualizarPosicao()
    {
        Transform casaP1 = casas[posPlayer1];
        Transform casaP2 = casas[posPlayer2];

        Transform pontoP1 = casaP1.Find("Player1");
        Transform pontoP2 = casaP2.Find("Player2");

        if (pontoP1 != null)
        {
            player1.SetParent(pontoP1);
            player1.localPosition = Vector3.zero;
            player1.localRotation = Quaternion.Euler(0, 0, 90);
        }

        if (pontoP2 != null)
        {
            player2.SetParent(pontoP2);
            player2.localPosition = Vector3.zero;
            player2.localRotation = Quaternion.Euler(0, 0, -90);
        }
    }

    void AtivarCameraPlayer()
    {
        mainCamera.gameObject.SetActive(false);
        if (turnoPlayer1) cameraPlayer1.gameObject.SetActive(true);
        else cameraPlayer2.gameObject.SetActive(true);
    }

    void VoltarCameraPrincipal()
    {
        mainCamera.gameObject.SetActive(true);
        cameraPlayer1.gameObject.SetActive(false);
        cameraPlayer2.gameObject.SetActive(false);
    }
}