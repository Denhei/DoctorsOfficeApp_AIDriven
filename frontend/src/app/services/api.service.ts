import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface Instituicao {
  id: string;
  nome: string;
  razaoSocial: string;
  ativa: boolean;
}

export interface Medico {
  id: string;
  usuarioId: string;
  nome: string;
  especialidade: string;
  crmFicticio: string;
  instituicaoId: string;
  instituicaoNome: string;
}

export interface SlotAgenda {
  id: string;
  medicoId: string;
  medicoNome: string;
  especialidade: string;
  instituicaoId: string;
  instituicaoNome: string;
  inicioUtc: string;
  fimUtc: string;
  disponivel: boolean;
}

export interface Consulta {
  id: string;
  pacienteId: string;
  pacienteNome: string;
  medicoId: string;
  medicoNome: string;
  especialidade: string;
  instituicaoId: string;
  instituicaoNome: string;
  slotId: string;
  inicioUtc: string;
  fimUtc: string;
  coletaId: string;
  resumoValidado?: string;
  status: 'Agendada' | 'EmAtendimento' | 'Concluida' | 'Cancelada';
  criadaEmUtc: string;
  temEvolucao: boolean;
  temPesquisaRespondida: boolean;
}

export interface ConversaDetalhe {
  coletaId: string;
  conversaId: string;
  pacienteId: string;
  pacienteNome: string;
  instituicaoId: string;
  instituicaoNome: string;
  statusColeta: 'EmColeta' | 'PendenteValidacao' | 'Validada' | 'EncaminhadaPresencial';
  etapaAtual: number;
  resumoPreliminar: {
    queixa: string;
    duracao: string;
    alergias: string;
    medicamentos: string;
    observacoes: string;
  };
  resumoValidado?: string;
  mensagens: Array<{
    id: string;
    remetente: 'Bot' | 'Paciente';
    conteudo: string;
    tipo: string;
    opcoes?: string[];
    enviadaEmUtc: string;
  }>;
  criadaEmUtc: string;
}

export interface ColetaItemFila {
  id: string;
  pacienteId: string;
  pacienteNome: string;
  instituicaoId: string;
  instituicaoNome: string;
  status: string;
  criadaEmUtc: string;
  resumoPreliminarTexto?: string;
}

export interface Evolucao {
  id: string;
  consultaId: string;
  medicoId: string;
  medicoNome: string;
  texto: string;
  criadaEmUtc: string;
}

export interface DocumentoPaciente {
  id: string;
  pacienteId: string;
  instituicaoOrigemId: string;
  instituicaoOrigemNome: string;
  nomeOriginal: string;
  tipo: string;
  extensao: string;
  tamanhoBytes: number;
  criadoEmUtc: string;
}

export interface Autorizacao {
  id: string;
  pacienteId: string;
  pacienteNome: string;
  instituicaoOrigemId: string;
  instituicaoOrigemNome: string;
  instituicaoDestinoId: string;
  instituicaoDestinoNome: string;
  criadaEmUtc: string;
  revogadaEmUtc?: string;
  ativa: boolean;
}

export interface HistoricoPaciente {
  pacienteId: string;
  nome: string;
  dataNascimento: string;
  consultas: Consulta[];
  evolucoes: Evolucao[];
  documentos: DocumentoPaciente[];
  autorizacoesAtivas: Autorizacao[];
}

export interface Notificacao {
  id: string;
  pacienteId: string;
  consultaId?: string;
  canal: 'WhatsApp' | 'Email';
  finalidade: string;
  titulo: string;
  texto: string;
  criadaEmUtc: string;
  lidaEmUtc?: string;
}

export interface PesquisaSatisfacao {
  id: string;
  consultaId: string;
  pacienteId: string;
  nota: number;
  percepcao: string;
  comentario?: string;
  respondidaEmUtc: string;
}

export interface DashboardOperacional {
  totalConsultasAgendadas: number;
  totalConsultasEmAtendimento: number;
  totalConsultasConcluidas: number;
  totalConsultasCanceladas: number;
  totalColetasPendentesValidacao: number;
  totalPesquisasRespondidas: number;
  mediaSatisfacao: number;
}

@Injectable({
  providedIn: 'root'
})
export class ApiService {
  private readonly baseUrl = 'http://localhost:5000/api';

  constructor(private http: HttpClient) {}

  // Instituições & Médicos
  getInstituicoes(): Observable<Instituicao[]> {
    return this.http.get<Instituicao[]>(`${this.baseUrl}/instituicoes`);
  }

  getMedicos(instituicaoId?: string): Observable<Medico[]> {
    const params = instituicaoId ? `?instituicaoId=${instituicaoId}` : '';
    return this.http.get<Medico[]>(`${this.baseUrl}/medicos${params}`);
  }

  // Coleta & Chat
  iniciarColeta(instituicaoId: string): Observable<ConversaDetalhe> {
    return this.http.post<ConversaDetalhe>(`${this.baseUrl}/coletas`, { instituicaoId });
  }

  getColeta(coletaId: string): Observable<ConversaDetalhe> {
    return this.http.get<ConversaDetalhe>(`${this.baseUrl}/coletas/${coletaId}`);
  }

  enviarMensagemChat(coletaId: string, conteudo: string, clienteMessageId?: string): Observable<ConversaDetalhe> {
    return this.http.post<ConversaDetalhe>(`${this.baseUrl}/coletas/${coletaId}/mensagens`, {
      conteudo,
      clienteMessageId: clienteMessageId || `msg-${Date.now()}`
    });
  }

  finalizarColeta(coletaId: string): Observable<ConversaDetalhe> {
    return this.http.post<ConversaDetalhe>(`${this.baseUrl}/coletas/${coletaId}/finalizacao`, {});
  }

  // Fila Enfermeiro
  getFilaEnfermeiro(): Observable<ColetaItemFila[]> {
    return this.http.get<ColetaItemFila[]>(`${this.baseUrl}/coletas?status=PendenteValidacao`);
  }

  validarColeta(coletaId: string, dados: { aprovadoParaAgendamento: boolean; resumoCorrigido: string; observacaoEnfermeiro?: string }): Observable<any> {
    return this.http.put(`${this.baseUrl}/coletas/${coletaId}/validacao`, dados);
  }

  // Slots & Agenda
  getSlots(medicoId?: string, instituicaoId?: string, data?: string): Observable<SlotAgenda[]> {
    let query = [];
    if (medicoId) query.push(`medicoId=${medicoId}`);
    if (instituicaoId) query.push(`instituicaoId=${instituicaoId}`);
    if (data) query.push(`data=${data}`);
    const qs = query.length ? `?${query.join('&')}` : '';
    return this.http.get<SlotAgenda[]>(`${this.baseUrl}/slots${qs}`);
  }

  criarSlot(dados: { medicoId: string; instituicaoId: string; inicioUtc: string; fimUtc: string }): Observable<SlotAgenda> {
    return this.http.post<SlotAgenda>(`${this.baseUrl}/slots`, dados);
  }

  // Consultas
  agendarConsulta(dados: { pacienteId: string; slotId: string; coletaId: string }): Observable<Consulta> {
    return this.http.post<Consulta>(`${this.baseUrl}/consultas`, dados);
  }

  getConsultas(instituicaoId?: string, pacienteId?: string, medicoId?: string): Observable<Consulta[]> {
    let query = [];
    if (instituicaoId) query.push(`instituicaoId=${instituicaoId}`);
    if (pacienteId) query.push(`pacienteId=${pacienteId}`);
    if (medicoId) query.push(`medicoId=${medicoId}`);
    const qs = query.length ? `?${query.join('&')}` : '';
    return this.http.get<Consulta[]>(`${this.baseUrl}/consultas${qs}`);
  }

  getConsulta(id: string): Observable<Consulta> {
    return this.http.get<Consulta>(`${this.baseUrl}/consultas/${id}`);
  }

  atualizarStatusConsulta(id: string, novoStatus: string): Observable<any> {
    return this.http.patch(`${this.baseUrl}/consultas/${id}/status`, { novoStatus });
  }

  registrarEvolucao(consultaId: string, texto: string): Observable<Evolucao> {
    return this.http.put<Evolucao>(`${this.baseUrl}/consultas/${consultaId}/evolucao`, { texto });
  }

  concluirConsulta(consultaId: string): Observable<Consulta> {
    return this.http.post<Consulta>(`${this.baseUrl}/consultas/${consultaId}/conclusao`, {});
  }

  // Histórico & Documentos
  getHistoricoPaciente(pacienteId: string): Observable<HistoricoPaciente> {
    return this.http.get<HistoricoPaciente>(`${this.baseUrl}/pacientes/${pacienteId}/historico`);
  }

  anexarDocumento(pacienteId: string, instituicaoOrigemId: string, arquivo: File): Observable<DocumentoPaciente> {
    const formData = new FormData();
    formData.append('arquivo', arquivo);
    return this.http.post<DocumentoPaciente>(`${this.baseUrl}/pacientes/${pacienteId}/documentos?instituicaoOrigemId=${instituicaoOrigemId}`, formData);
  }

  baixarDocumento(documentoId: string): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/documentos/${documentoId}/arquivo`, { responseType: 'blob' });
  }

  // Compartilhamento
  getAutorizacoes(pacienteId: string): Observable<Autorizacao[]> {
    return this.http.get<Autorizacao[]>(`${this.baseUrl}/pacientes/${pacienteId}/autorizacoes`);
  }

  concederAutorizacao(pacienteId: string, instituicaoOrigemId: string, instituicaoDestinoId: string): Observable<Autorizacao> {
    return this.http.post<Autorizacao>(`${this.baseUrl}/pacientes/${pacienteId}/autorizacoes`, {
      instituicaoOrigemId,
      instituicaoDestinoId
    });
  }

  revogarAutorizacao(autorizacaoId: string): Observable<any> {
    return this.http.delete(`${this.baseUrl}/autorizacoes/${autorizacaoId}`);
  }

  // Notificações & Pesquisa
  getNotificacoes(): Observable<Notificacao[]> {
    return this.http.get<Notificacao[]>(`${this.baseUrl}/notificacoes`);
  }

  getPesquisa(consultaId: string): Observable<PesquisaSatisfacao> {
    return this.http.get<PesquisaSatisfacao>(`${this.baseUrl}/consultas/${consultaId}/pesquisa`);
  }

  responderPesquisa(consultaId: string, dados: { nota: number; percepcao: string; comentario?: string }): Observable<PesquisaSatisfacao> {
    return this.http.put<PesquisaSatisfacao>(`${this.baseUrl}/consultas/${consultaId}/pesquisa`, dados);
  }

  // Dashboard
  getDashboard(): Observable<DashboardOperacional> {
    return this.http.get<DashboardOperacional>(`${this.baseUrl}/dashboard`);
  }

  // Admin
  getUsuariosAdmin(): Observable<any[]> {
    return this.http.get<any[]>(`${this.baseUrl}/admin/usuarios`);
  }

  getHealth(): Observable<any> {
    return this.http.get<any>(`${this.baseUrl}/health`);
  }
}
