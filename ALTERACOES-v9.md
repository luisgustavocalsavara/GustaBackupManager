# GustaBackupManager v9 – alterações solicitadas

## 1. Cadastro de Tipos de Arquivo – seleção de ícones
- Adicionado campo de busca por nome/chave do ícone.
- A lista de ícones agora é filtrada enquanto o usuário digita.
- A área de seleção ficou mais alta e com rolagem, facilitando localizar ícones em catálogos grandes.
- Mantida a seleção visual do ícone escolhido e a ação de cadastrar ícone personalizado.

## 2. Cadastro de Ícone e Cor por Categoria de Pasta
- Removido o evento de seleção da linha da tabela que carregava imediatamente os dados no formulário.
- Agora a categoria somente entra no formulário de edição quando o usuário clica explicitamente no botão Editar da coluna Ações.
- A seleção acidental da linha não ativa mais o modo de edição.

## Observação sobre o catálogo de componentes
O cadastro pré-existente de modelos (categoria, tipo, marca e modelo) e sua atribuição à pasta é uma funcionalidade nova que exige persistência e interface CRUD. Não foi incluída nesta revisão para evitar introduzir um esquema de dados incompleto sem validação do fluxo de edição/compatibilidade dos metadados existentes.
