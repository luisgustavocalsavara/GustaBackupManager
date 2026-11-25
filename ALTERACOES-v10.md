# GustaBackupManager v10 — Catálogo de Dispositivos

## Implementado
- Cadastro persistente de dispositivos no SQLite (`CatalogoDispositivos`).
- Categorias vinculadas às categorias de componente já existentes (CLP, IHM, Inversor, Motion, Instrumento, Safe, Rede, Switch, Robô e Dispositivo Dedicado).
- Campos: Tipo, Marca, Modelo e Descrição/Observações.
- Cadastro, edição, exclusão, filtro por categoria e busca.
- Catálogo acessível pelo menu Cadastros e Rede; edição limitada a Administradores.
- Seed inicial com exemplos de dispositivos PLC, IHM, Switch, Inversor, Servo, Robô, Instrumento e Gateway. Esses exemplos podem ser editados ou removidos.
- Na criação/edição de pasta do grupo Componente, o catálogo é filtrado pela categoria selecionada. A seleção preenche Tipo, Marca e Modelo.
- O ID do item do catálogo e os valores descritivos são gravados em `folder.meta.json`; arquivos de metadados anteriores continuam compatíveis, pois os campos novos são opcionais.
- Se um item for excluído do catálogo, pastas já vinculadas preservam o snapshot de Tipo/Marca/Modelo e seu ID gravado.

## Observação de validação
O ambiente de geração não possui SDK .NET instalado, portanto o build/compilação precisa ser confirmado no Visual Studio com .NET 10.
