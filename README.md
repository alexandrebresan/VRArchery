# VRArchery

VRArchery é um jogo de arco e flecha em realidade virtual desenvolvido na Unity para Meta Quest, como projeto da disciplina Multimídia e Realidade Virtual. No Vale dos Arqueiros, o jogador segura o arco com a mão esquerda, puxa a corda com a direita, mira e solta para disparar.

## Funcionalidades

- **Treino Livre.** Prática sem limite de tempo ou flechas, com pontuação total, acertos, erros, precisão e contagem por região. A troca de dificuldade aguarda o resultado dos tiros em voo.
- **Desafio por Rodadas.** Quatro rodadas de três flechas. Cada rodada começa com multiplicador ×1. Acertar o centro dobra o multiplicador da próxima flecha, até ×4. Um tiro fora do centro usa o multiplicador atual e faz o próximo voltar a ×1. O jogo mostra o resultado entre rodadas e o placar ao final.
- **Pontuação.** As cinco regiões valem 10 pontos no centro amarelo, 8 no vermelho, 6 no azul, 4 no preto e 2 no branco. Um erro vale 0.
- **Dificuldades.** No Fácil, o alvo fica parado a 6 m. No Médio, move-se na horizontal a 8 m. No Difícil, combina movimento horizontal e vertical a 10 m. A terceira rodada do desafio usa uma variação mais rápida do Médio.
- **Disparo e feedback.** A puxada determina a velocidade inicial. A flecha segue uma trajetória sob gravidade, colide com a superfície atingida e acompanha o alvo se ficar presa nele. A ambientação medieval inclui sons de interação e um marcador visual de impacto.

## Tecnologias

| Tecnologia | Versão |
| --- | --- |
| Unity | 6000.4.9f1 |
| C# | 9.0 |
| XR Interaction Toolkit | 3.4.1 |
| OpenXR Plugin / Unity OpenXR Meta | 1.16.1 / 2.5.0 |
| XR Plug-in Management / XR Core Utilities | 4.5.4 / 2.6.0 |
| Universal Render Pipeline | 17.4.0 |
| Input System | 1.19.0 |
| Unity UI (inclui TextMesh Pro) | 2.0.0 |
| XR Hands / AR Foundation | 1.7.3 / 6.5.0 |

As dependências são gerenciadas pelo Unity Package Manager.

## Executar no Unity

1. Clone o repositório.

   ```bash
   git clone https://github.com/alexandrebresan/VRArchery.git
   cd VRArchery
   ```

2. Instale a **Unity 6000.4.9f1** pelo Unity Hub. Use **Add > Add project from disk** para abrir a pasta clonada.
3. Aguarde a restauração dos pacotes e a importação dos assets. Se houver falha de dependência, confira o **Package Manager**.
4. Abra a [GameScene](Assets/VRArchery/Scenes/Stage7/GameScene.unity), entre em **Play Mode** e dê foco à aba **Game** para usar o simulador.

### Controles no Meta Quest

- Aponte o raio para o painel e pressione o gatilho para selecionar.
- Segure a empunhadura com o grip esquerdo. Aproxime o controle direito da corda, aperte o grip direito e recue a mão.
- Mantenha o grip esquerdo pressionado, mire e solte o direito após puxar pelo menos 8 cm.

### Controles no XR Interaction Simulator

1. Se estiver em modo FPS, pressione **Tab** para voltar a **Controller**. Clique no painel com o mouse ou selecione o controle direito com **`]`**, mire com o botão direito do mouse pressionado e use **T** como gatilho.
2. Selecione o esquerdo com **`[`**. Use **W/A/S/D** e **Q/E** para posicionar a mão. Pressione **Shift + G** para pegar o arco e solte **Shift** antes de **G** para manter o grip esquerdo travado.
3. Selecione o direito com **`]`** e aproxime-o da corda até o indicador ficar verde. Segure **G**, recue com **S** e solte **G** para disparar.

## Build e instalação no Meta Quest

1. No Unity Hub, adicione **Android Build Support**, **Android SDK & NDK Tools** e **OpenJDK** à instalação da Unity. O build foi gerado com SDK 36, NDK 27.2.12479018 (r27c) e OpenJDK 17.0.18. [Configuração Android na Unity](https://docs.unity3d.com/6000.4/Documentation/Manual/android-sdksetup.html)
2. Em **File > Build Profiles**, ative **MetaQuest**. Confira a GameScene na lista de cenas e as configurações **IL2CPP**, **ARM64**, **Vulkan**, API mínima **32** e API alvo **Automatic**.
3. Com **Build App Bundle** desativado, clique em **Build** e salve o APK em `Builds/MetaQuest/VRArchery.apk`.
4. No headset autorizado, habilite o modo desenvolvedor, conecte um cabo USB de dados e aceite a depuração USB. [Guia da Meta](https://developers.meta.com/vr/documentation/android-apps/enable-developer-mode/)
5. Na raiz do projeto, use o `adb` do Android SDK Platform Tools para instalar o APK. Depois, abra **Biblioteca > Fontes desconhecidas** no headset e selecione **VRArchery**.

   ```bash
   adb devices
   adb install -r Builds/MetaQuest/VRArchery.apk
   ```

## Demonstração

O jogo foi instalado e executado em um Meta Quest 3S.

[**Assistir à demonstração do VRArchery no Meta Quest 3S**](https://horizon.meta.com/shares/mTV0iUIoNojeUCWs4g7VPwVXfMwR4s)
