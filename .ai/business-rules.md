# Business Rules

## 1. Triagem (Chat)
- O fluxo de triagem é determinístico; a sequência de perguntas é fixa e armazenada em **MongoDB** na coleção `conversas`.
- Cada resposta do paciente deve ser validada contra o modelo de DTO `EnviarMensagemChatDto`.
- O resumo final da triagem (`ResumoCorrigido`) só pode ser aprovado por um **Enfermeiro** antes de avançar para agendamento.

## 2. Validação Enfermeiro
- Somente usuários com a role **Enfermeiro** podem acessar a rota `/api/enfermeiro/validar`.
- A validação pode **aprovar** ou **rejeitar** a coleta; em caso de rejeição, o paciente deve retomar a triagem.

## 3. Agendamento de Consulta
- Slots (`SlotAgenda`) são pré‑gerados e marcados como `Disponivel = true`.
- Ao reservar um slot, ele deve ser marcado como `Disponivel = false` dentro de uma transação para evitar dupla reserva.
- Só pacientes que tenham **triagem aprovada** podem reservar.

## 4. Evolução Clínica
- Apenas o médico responsável (`MedicoId`) pode registrar ou atualizar a entidade `Evolucao` da consulta.
- A evolução pode ser **finalizada** somente quando todos os campos obrigatórios estiverem preenchidos.

## 5. Notificações
- Notificações são criadas automaticamente ao concluir a consulta ou ao mudar o status de uma coleta.
- O tipo de notificação (`WhatsApp`, `Email`) é definido pelo canal preferencial do paciente.

## 6. Pesquisa de Satisfação
- Uma pesquisa pode ser enviada **apenas uma vez** por consulta concluída.
- O rating (1‑5 estrelas) e comentários são armazenados na entidade `PesquisaSatisfacao`.

## 7. Controle de Acesso (RBAC)
- **Administrador**: acesso total a todas as rotas e recursos.
- **Atendente**: acesso ao dashboard, listagem de consultas, criação/edição de agendamentos.
- **Enfermeiro**: acesso à fila de coletas e validação.
- **Médico**: acesso às consultas atribuídas, registro de evolução e visualização de histórico do paciente.
- **Paciente**: acesso somente aos seus próprios recursos (triagem, agenda, histórico, notificações, pesquisa).

## 8. Integridade dos Dados
- Operações que afetam múltiplas tabelas (ex.: reserva de slot + criação de consulta) devem ser executadas dentro de uma **transação EF Core**.
- Em caso de falha, a transação é revertida garantindo consistência.

---

