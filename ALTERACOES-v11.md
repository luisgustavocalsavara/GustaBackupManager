# GustaBackupManager v11 - Catálogo de Projetos

## Mudança de conceito

O cadastro de Dispositivos/Componentes foi substituído na interface e no fluxo de pastas por um Catálogo de Projetos de Engenharia. As pastas agora associam um projeto/software e herdam a lista de extensões permitidas.

## Catálogo

- Categorias: Projeto CLP, Projeto IHM, Projeto Rede, Motion, Robô, Safety, Drives e Outros.
- Nome do software/ambiente.
- Extensões permitidas (ex.: `.acd;.l5x;.l5k`).
- Descrição.
- Inclusão, edição, pesquisa, filtro e exclusão.
- Cadastro restrito a Administradores.

## Exemplos semeados

Studio 5000, RSLogix 500, TIA Portal, CODESYS, FactoryTalk View, TIA WinCC e RSNetwork.

## Pasta

- Novo grupo `Projeto`.
- Seleciona categoria e projeto do catálogo.
- A pasta aceita somente as extensões associadas ao projeto (além de snapshots de extensões em caso de catálogo removido).
- O metadata JSON grava ID, categoria, nome e extensões do projeto.
- Pastas antigas `Componente` são convertidas para o novo grupo `Projeto` ao editar, preservando as extensões anteriormente permitidas como extensões adicionais durante a migração.
- `Documentação` continua utilizando o cadastro de Tipos de Arquivo.

## Compatibilidade

O SQLite existente recebe a tabela `CatalogoProjetos` por `CREATE TABLE IF NOT EXISTS`, pois `EnsureCreated()` não atualiza bancos existentes. O antigo cadastro de dispositivos não é mais utilizado pela aplicação; a tabela legada não é apagada automaticamente.

## Validação

ZIP verificado estruturalmente. Build não foi executado por ausência do SDK .NET neste ambiente.
