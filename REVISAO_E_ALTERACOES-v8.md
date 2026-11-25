# GustaBackupManager v8 - Revisão e alterações

## Alterações aplicadas

### Segurança
- Hash de senha atualizado para PBKDF2-SHA256 com salt aleatório individual e 600.000 iterações.
- Compatibilidade com hashes SHA-256 legados da versão 7: quando o login é válido, o hash é migrado para PBKDF2.
- Comparação de hashes realizada com `CryptographicOperations.FixedTimeEquals`.
- Cadastro, listagem e exclusão de usuários protegidos por verificação de administrador no serviço.
- Exclusão do último administrador impedida; o usuário conectado também não pode excluir a própria conta.
- Sanitização de SVG alterada para parsing XML com DTD desabilitado e lista de elementos/atributos permitidos. Referências externas são removidas.
- DevTools do WebView2 habilitado somente em compilação DEBUG.
- Validação de caminho dentro da raiz SMB adicionada à listagem de arquivos e download.

### Confiabilidade de arquivos
- Upload passa a copiar primeiro para arquivo temporário e só depois renomear para o destino definitivo.
- Download passa a usar arquivo temporário local e só substitui o destino após concluir a cópia.
- Metadados JSON são gravados em arquivo temporário e substituídos após a gravação concluída.
- Arquivos temporários são removidos em tentativa de limpeza (best-effort).

## Observações importantes
- O build não foi executado: o ambiente de geração não possui o SDK `dotnet` instalado.
- Recomenda-se abrir a solução no Visual Studio com o SDK .NET 10 e executar Build antes de substituir a versão em produção.
- As credenciais padrão existentes na inicialização continuam presentes para preservar o fluxo atual. Troque-as imediatamente após o primeiro acesso.
- O carregamento recursivo da árvore SMB ainda está presente. Lazy loading/cache é uma próxima melhoria de desempenho recomendada, mas não foi implementada nesta entrega para evitar mudança maior no comportamento da interface.
- A gravação de arquivo e metadados não constitui uma transação única: se a gravação do JSON falhar depois do arquivo definitivo ser publicado, poderá haver um arquivo sem registro no JSON. O arquivo, contudo, não ficará parcialmente copiado sob o nome final.
