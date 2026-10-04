# Prompt para gerar a especificação da POC

Copie todo o conteúdo entre `INÍCIO DO PROMPT` e `FIM DO PROMPT` e envie à IA responsável pelo planejamento.

---

## INÍCIO DO PROMPT

Atue como arquiteto de software e analista de requisitos, com experiência em Angular, ASP.NET Core, PostgreSQL e MongoDB.

Sua tarefa é elaborar um arquivo Markdown que servirá como especificação e conjunto de instruções para outro agente de IA implementar uma POC acadêmica. Não implemente a aplicação agora. Primeiro transforme as informações abaixo em um plano técnico claro, coerente, simples e verificável.

### Objetivo

Planejar uma POC de sistema de triagem inicial e agendamento para clínicas e hospitais. O sistema pretende demonstrar como reduzir esperas, retrabalho e falhas de comunicação ao longo da jornada do paciente.

A atividade faz parte de uma disciplina de pós-graduação. A prioridade é demonstrar um fluxo funcional durante uma apresentação em aula. Evite complexidade de produção que não gere valor visível para a demonstração.

### Problemas observados

- Preenchimento repetido de dados.
- Cadastro manual.
- Conferência de documentos.
- Autorização de convênio.
- Espera entre recepção e triagem.
- Espera entre triagem e médico.
- Médico aguardando informações do paciente.
- Médico aguardando sistema ou prontuário.
- Pagamento.
- Emissão de documentos.
- Agendamento de retorno.
- Comunicação fragmentada entre setores.

Esses problemas representam o contexto geral. A POC não precisa resolver todos eles. Selecione um recorte pequeno que demonstre valor de ponta a ponta.

### Funcionalidades desejadas

- Chat de triagem inicial com aparência de WhatsApp.
- O chat deve ser uma simulação determinística, com perguntas predefinidas, sem LLM e sem integração real com WhatsApp.
- A coleta inicial deve ser encaminhada para validação de um enfermeiro.
- Agendamento conforme disponibilidade do profissional e escolha do paciente.
- Cadastro único do paciente para evitar preenchimento repetido.
- Histórico médico e documentos compartilháveis entre médicos, clínicas ou hospitais mediante autorização do paciente.
- Interface web para paciente, atendente, enfermeiro, médico e administrador técnico/desenvolvedor.
- Pesquisa de satisfação após a consulta, com WhatsApp e e-mail simulados dentro da aplicação.
- A pesquisa pode perguntar como o paciente se sente após a consulta ou tratamento, mas não deve ser apresentada como avaliação clínica automática.

### Usuários mínimos

- Paciente.
- Atendente.
- Enfermeiro.
- Médico.
- Desenvolvedor ou administrador técnico.

Defina claramente o que cada perfil pode visualizar e alterar. Diferencie dados administrativos de dados clínicos. As permissões precisam ser validadas no backend, não somente escondidas na interface.

### Tecnologias obrigatórias

- Frontend: Angular.
- Backend: ASP.NET Core Web API.
- Banco relacional: PostgreSQL.
- Banco documental: MongoDB.

Atribua uma responsabilidade clara a cada banco e evite duplicar a mesma fonte de verdade nos dois. Prefira monólito modular. Não use microserviços, mensageria, CQRS, event sourcing, Kubernetes, banco vetorial, RAG, modelos de IA ou integrações pagas.

### Restrições de escopo

- Execução local.
- Sem serviços pagos.
- Sem LLM ou Ollama.
- Sem integração real com WhatsApp, e-mail, convênios ou prontuários externos.
- WhatsApp e e-mail devem ser claramente identificados como simulações.
- Usar somente dados fictícios.
- Não apresentar o sistema como ferramenta de diagnóstico ou protocolo clínico validado.
- O chatbot não deve diagnosticar, prescrever, classificar urgência nem autorizar atendimento sozinho.
- A validação da coleta deve ser feita por um enfermeiro.
- Segurança suficiente para demonstrar autorização por perfil, sem tentar reproduzir toda a infraestrutura de um hospital real.
- Não adicionar funcionalidades apenas para tornar a arquitetura mais sofisticada.

### Fluxo principal esperado

Planeje um único fluxo demonstrável de ponta a ponta:

1. Paciente entra com uma conta fictícia e confirma seus dados já cadastrados.
2. Paciente responde a uma coleta inicial guiada.
3. O sistema gera um resumo por template, sem IA.
4. Enfermeiro revisa, corrige e valida a coleta.
5. Após a validação, o paciente escolhe um horário disponível.
6. Médico acessa a consulta e o histórico permitido.
7. Médico registra uma evolução simples e conclui o atendimento.
8. O sistema cria um convite simulado para pesquisa de satisfação.
9. Paciente responde à pesquisa.

O compartilhamento entre duas instituições fictícias pode ser demonstrado como fluxo secundário: o paciente concede autorização, um médico da segunda instituição com vínculo válido acessa o histórico permitido e, depois da revogação, novos acessos são bloqueados.

### Como conduzir a tarefa

1. Analise as informações antes de redigir o documento.
2. Identifique decisões ausentes que realmente impeçam uma especificação coerente.
3. Faça no máximo cinco perguntas curtas e objetivas, somente se as respostas alterarem significativamente arquitetura, escopo ou critérios de aceite.
4. Para cada pergunta, sugira uma opção recomendada voltada à simplicidade.
5. Se as informações forem suficientes, não faça perguntas apenas por formalidade.
6. Quando precisar assumir algo, registre a suposição explicitamente e escolha a alternativa mais simples.
7. Depois das respostas, ou imediatamente se não houver bloqueios, produza o documento completo.

### Entregável obrigatório

Gere um único arquivo Markdown chamado `poc-clinica-instrucoes-agent.md`, escrito em português brasileiro. Ele deve ser autocontido e pronto para ser entregue a outro agente de IA responsável pela implementação.

O documento deve conter, nesta ordem:

1. Objetivo da POC.
2. Problema e recorte escolhido.
3. Escopo incluído.
4. Itens explicitamente fora do escopo.
5. Premissas e simplificações.
6. Perfis de usuários e matriz de permissões.
7. Fluxo principal e fluxo secundário de compartilhamento.
8. Regras de negócio e transições de estado.
9. Arquitetura em camadas: frontend, backend e dados.
10. Diagrama de arquitetura em Mermaid.
11. Tecnologias e responsabilidade de cada componente.
12. Entidades principais, campos essenciais e relacionamentos.
13. Diagrama entidade-relacionamento em Mermaid.
14. Separação dos dados entre PostgreSQL e MongoDB.
15. Principais endpoints REST, com método, rota, finalidade e perfis autorizados.
16. Telas mínimas por perfil.
17. Funcionamento detalhado do chatbot simulado.
18. Dados fictícios necessários para a demonstração.
19. Estratégia incremental de implementação.
20. Estrutura sugerida de pastas e projetos.
21. Instruções mínimas de execução local.
22. Critérios de aceite objetivos e testáveis.
23. Roteiro de apresentação de cinco a dez minutos.
24. Riscos, limitações e possíveis evoluções futuras.

### Requisitos de qualidade do documento

- Escreva instruções concretas, evitando frases genéricas como “implementar segurança adequada”.
- Explique quais verificações mínimas de autorização o backend deve fazer.
- Mantenha o número de entidades, telas e endpoints compatível com uma POC acadêmica.
- Não transforme cada funcionalidade em um serviço separado.
- Não invente integrações reais.
- Diferencie claramente o que será funcional do que será simulado.
- Proponha dados seed e credenciais exclusivamente fictícias.
- Inclua respostas HTTP esperadas para validação, autenticação, autorização, recurso inexistente e conflitos de agenda.
- Inclua prevenção simples de reserva duplicada do mesmo horário.
- Inclua idempotência apenas onde ela é relevante para a demonstração, sem criar infraestrutura excessiva.
- Inclua uma mensagem fixa orientando o paciente a procurar atendimento de emergência quando necessário, sem tentar detectar urgência por palavras-chave.
- Use datas em UTC no backend e apresente-as no fuso `America/Sao_Paulo` na interface.
- Não afirme conformidade integral com LGPD, validade clínica ou prontuário eletrônico certificado. Apenas registre essas questões como limitações de uma POC.
- Prefira tabelas para permissões, tecnologias, entidades e endpoints.
- Use diagramas Mermaid válidos e compactos.
- Evite repetir as mesmas instruções em seções diferentes.

### Decisões recomendadas, caso nenhuma informação adicional seja fornecida

- Monólito modular com uma SPA Angular e uma API ASP.NET Core.
- PostgreSQL como fonte de verdade para usuários, instituições, pacientes, agenda, consultas, evoluções, autorizações e pesquisas.
- MongoDB somente para conversas do chatbot, mensagens e estado da coleta guiada.
- Arquivos fictícios armazenados em volume local; apenas os metadados ficam no PostgreSQL.
- ASP.NET Core Identity e JWT simples para autenticação.
- Duas instituições fictícias.
- Um médico por instituição, um enfermeiro e um atendente por instituição, dois pacientes e um administrador técnico.
- Slots de consulta predefinidos com duração de 30 minutos.
- Agenda liberada somente após validação do enfermeiro.
- Chatbot como máquina de estados com perguntas fixas e resumo por template.
- Docker Compose apenas para PostgreSQL e MongoDB; frontend e API podem rodar localmente.
- Documentação REST com OpenAPI/Swagger.

### Formato da resposta

Se houver perguntas indispensáveis, responda somente com as perguntas numeradas e respectivas recomendações. Não gere uma especificação parcial.

Se não houver perguntas indispensáveis, responda com:

1. Uma frase curta informando as principais decisões assumidas.
2. O conteúdo completo do arquivo `poc-clinica-instrucoes-agent.md` em um único bloco Markdown.

Não entregue código da aplicação nesta etapa. Não omita seções alegando que serão definidas depois. Quando algo não for necessário para a POC, registre-o explicitamente como fora do escopo.

## FIM DO PROMPT

