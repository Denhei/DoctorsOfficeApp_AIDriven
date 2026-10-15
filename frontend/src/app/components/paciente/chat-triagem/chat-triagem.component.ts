import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ApiService, Instituicao, ConversaDetalhe, MensagemChat } from '../../../services/api.service';
import { AuthService } from '../../../services/auth.service';

@Component({
  selector: 'app-chat-triagem',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="chat-container">
      <!-- Cabeçalho do Chat estilo WhatsApp -->
      <div class="chat-header">
        <div class="header-left">
          <div class="avatar">🤖</div>
          <div class="header-info">
            <h3>Assistente de Triagem Guiada</h3>
            <span class="status-indicator">● Coleta Determinística (Sem IA)</span>
          </div>
        </div>
        <div class="header-right">
          <span class="badge badge-warning">Canal simulado — coleta guiada sem IA</span>
        </div>
      </div>

      <!-- Área de Mensagens -->
      <div class="chat-messages" #scrollContainer>
        <!-- Aviso no início -->
        <div class="chat-notice">
          ℹ️ Esta coleta de sintomas é guiada por um roteiro determinístico fixo. As informações serão revisadas por um enfermeiro antes de liberar seu agendamento. Em situações de urgência, procure um pronto-socorro.
        </div>

        <div *ngIf="!conversa && !loading" class="start-session-card">
          <h4>Selecione a instituição para iniciar o atendimento:</h4>
          <div class="inst-options">
            <button 
              *ngFor="let inst of instituicoes" 
              class="btn btn-primary"
              (click)="iniciarNovaColeta(inst.id)">
              🏥 {{ inst.nome }}
            </button>
          </div>
        </div>

        <div *ngFor="let msg of conversa?.mensagens" class="message-wrapper" [ngClass]="msg.remetente === 'Paciente' ? 'paciente' : 'bot'">
          <div class="message-bubble">
            <div class="message-content" [innerHTML]="formatarMensagem(msg.conteudo)"></div>
            <div class="message-time">{{ msg.enviadaEmUtc | date:'HH:mm' }}</div>
          </div>

          <!-- Botões de opções rápidas se a mensagem contiver -->
          <div *ngIf="msg.opcoes && msg.opcoes.length > 0 && isUltimaMensagem(msg) && conversa?.statusColeta === 'EmColeta'" class="quick-options">
            <button 
              *ngFor="let opt of msg.opcoes" 
              class="btn btn-secondary btn-sm"
              (click)="enviarOpcaoRapida(opt)">
              {{ opt }}
            </button>
          </div>
        </div>

        <div *ngIf="loading" class="typing-indicator">
          <span>●</span><span>●</span><span>●</span> Assistente processando...
        </div>
      </div>

      <!-- Resumo Preliminar e Confirmação (quando pronto) -->
      <div *ngIf="conversa && conversa.etapaAtual >= 9 && conversa.statusColeta === 'EmColeta'" class="summary-box">
        <h4>📋 Resumo Pronto para Envio</h4>
        <p><strong>Queixa:</strong> {{ conversa.resumoPreliminar.queixa }}</p>
        <p><strong>Duração:</strong> {{ conversa.resumoPreliminar.duracao }}</p>
        <p><strong>Alergias:</strong> {{ conversa.resumoPreliminar.alergias }}</p>
        <p><strong>Medicamentos:</strong> {{ conversa.resumoPreliminar.medicamentos }}</p>
        <p><strong>Observações:</strong> {{ conversa.resumoPreliminar.observacoes }}</p>
        
        <button class="btn btn-primary" (click)="finalizarEnvio()" [disabled]="loading">
          🚀 Enviar para o Enfermeiro e Aguardar Validação
        </button>
      </div>

      <!-- Status após envio -->
      <div *ngIf="conversa && conversa.statusColeta === 'PendenteValidacao'" class="alert alert-info">
        ⏳ <strong>Coleta enviada com sucesso!</strong> Um enfermeiro da unidade <em>{{ conversa.instituicaoNome }}</em> está revisando seus sintomas. Assim que validado, você poderá escolher o horário de sua consulta.
      </div>

      <div *ngIf="conversa && conversa.statusColeta === 'Validada'" class="alert alert-success flex justify-between items-center">
        <div>
          ✅ <strong>Triagem Validada pelo Enfermeiro!</strong><br />
          <small>{{ conversa.resumoValidado }}</small>
        </div>
        <button class="btn btn-primary btn-sm" (click)="irParaAgenda()">
          📅 Ir para Agendamento
        </button>
      </div>

      <div *ngIf="conversa && conversa.statusColeta === 'EncaminhadaPresencial'" class="alert alert-danger">
        ⚠️ <strong>Avaliação Presencial Recomendada:</strong> Conforme orientação do enfermeiro, dirija-se imediatamente à unidade médica para atendimento presencial.
      </div>

      <!-- Barra de Input -->
      <div class="chat-input-bar" *ngIf="conversa && conversa.statusColeta === 'EmColeta' && conversa.etapaAtual < 9">
        <input 
          type="text" 
          class="chat-input" 
          [(ngModel)]="textoInput" 
          (keyup.enter)="enviarTexto()"
          placeholder="Digite sua resposta..." 
          [disabled]="loading" />
        <button class="btn btn-primary" (click)="enviarTexto()" [disabled]="!textoInput.trim() || loading">
          Enviar ➤
        </button>
      </div>
    </div>
  `,
  styles: [`
    .chat-container {
      max-width: 850px;
      margin: 1.5rem auto;
      background: #efeae2;
      border-radius: 12px;
      border: 1px solid var(--border);
      display: flex;
      flex-direction: column;
      height: calc(100vh - 120px);
      box-shadow: 0 4px 6px -1px rgba(0,0,0,0.1);
      overflow: hidden;
    }
    .chat-header {
      background: #075e54;
      color: white;
      padding: 0.875rem 1.25rem;
      display: flex;
      align-items: center;
      justify-content: space-between;
    }
    .header-left {
      display: flex;
      align-items: center;
      gap: 0.75rem;
    }
    .avatar {
      width: 40px;
      height: 40px;
      background: #128c7e;
      border-radius: 50%;
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 1.25rem;
    }
    .header-info h3 {
      font-size: 1rem;
      font-weight: 600;
      margin: 0;
    }
    .status-indicator {
      font-size: 0.75rem;
      color: #bbf7d0;
    }
    .chat-messages {
      flex: 1;
      padding: 1.25rem;
      overflow-y: auto;
      display: flex;
      flex-direction: column;
      gap: 0.875rem;
    }
    .chat-notice {
      background: #fff3cd;
      border: 1px solid #ffeeba;
      color: #856404;
      padding: 0.625rem 0.875rem;
      border-radius: 8px;
      font-size: 0.8125rem;
      text-align: center;
    }
    .start-session-card {
      background: white;
      padding: 1.5rem;
      border-radius: 12px;
      text-align: center;
      margin: auto;
      max-width: 500px;
      box-shadow: 0 2px 4px rgba(0,0,0,0.05);
    }
    .inst-options {
      display: flex;
      gap: 1rem;
      justify-content: center;
      margin-top: 1rem;
    }
    .message-wrapper {
      display: flex;
      flex-direction: column;
      max-width: 80%;
    }
    .message-wrapper.paciente {
      align-self: flex-end;
      align-items: flex-end;
    }
    .message-wrapper.bot {
      align-self: flex-start;
      align-items: flex-start;
    }
    .message-bubble {
      padding: 0.625rem 0.875rem;
      border-radius: 10px;
      font-size: 0.875rem;
      line-height: 1.4;
      position: relative;
      box-shadow: 0 1px 1px rgba(0,0,0,0.05);
      white-space: pre-wrap;
    }
    .message-wrapper.bot .message-bubble {
      background: #ffffff;
      color: #111827;
      border-top-left-radius: 2px;
    }
    .message-wrapper.paciente .message-bubble {
      background: #d9fdd3;
      color: #111827;
      border-top-right-radius: 2px;
    }
    .message-time {
      font-size: 0.6875rem;
      color: #6b7280;
      text-align: right;
      margin-top: 0.25rem;
    }
    .quick-options {
      display: flex;
      flex-wrap: wrap;
      gap: 0.5rem;
      margin-top: 0.5rem;
    }
    .typing-indicator {
      font-size: 0.8125rem;
      color: #64748b;
      font-style: italic;
    }
    .summary-box {
      background: #ffffff;
      padding: 1rem 1.25rem;
      border-top: 1px solid var(--border);
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
      font-size: 0.875rem;
    }
    .summary-box h4 {
      margin-bottom: 0.25rem;
      color: var(--secondary);
    }
    .chat-input-bar {
      background: #f0f2f5;
      padding: 0.75rem 1rem;
      display: flex;
      gap: 0.75rem;
      align-items: center;
      border-top: 1px solid var(--border);
    }
    .chat-input {
      flex: 1;
      padding: 0.625rem 1rem;
      border: 1px solid var(--border);
      border-radius: 20px;
      font-size: 0.875rem;
      outline: none;
      background: white;
    }
    .chat-input:focus {
      border-color: #128c7e;
    }
  `]
})
export class ChatTriagemComponent implements OnInit {
  private api = inject(ApiService);
  private auth = inject(AuthService);
  private router = inject(Router);

  instituicoes: Instituicao[] = [];
  conversa: ConversaDetalhe | null = null;
  textoInput = '';
  loading = false;

  ngOnInit() {
    this.api.getInstituicoes().subscribe(res => this.instituicoes = res);
  }

  iniciarNovaColeta(instId: string) {
    this.loading = true;
    this.api.iniciarColeta(instId).subscribe({
      next: (res) => {
        this.conversa = res;
        this.loading = false;
      },
      error: (err) => {
        this.loading = false;
        alert(err.error?.mensagem || 'Erro ao iniciar coleta.');
      }
    });
  }

  enviarTexto() {
    if (!this.textoInput.trim() || !this.conversa) return;
    const msg = this.textoInput;
    this.textoInput = '';
    this.processarMensagem(msg);
  }

  enviarOpcaoRapida(opcao: string) {
    if (!this.conversa) return;
    this.processarMensagem(opcao);
  }

  private processarMensagem(conteudo: string) {
    if (!this.conversa) return;
    this.loading = true;
    this.api.enviarMensagemChat(this.conversa.coletaId, conteudo).subscribe({
      next: (res) => {
        this.conversa = res;
        this.loading = false;
      },
      error: (err) => {
        this.loading = false;
        alert(err.error?.mensagem || 'Erro ao enviar mensagem.');
      }
    });
  }

  finalizarEnvio() {
    if (!this.conversa) return;
    this.loading = true;
    this.api.finalizarColeta(this.conversa.coletaId).subscribe({
      next: (res) => {
        this.conversa = res;
        this.loading = false;
      },
      error: (err) => {
        this.loading = false;
        alert(err.error?.mensagem || 'Erro ao finalizar coleta.');
      }
    });
  }

  isUltimaMensagem(msg: MensagemChat): boolean {
    if (!this.conversa || !this.conversa.mensagens.length) return false;
    return this.conversa.mensagens[this.conversa.mensagens.length - 1].id === msg.id;
  }

  formatarMensagem(texto: string): string {
    return texto.replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>');
  }

  irParaAgenda() {
    this.router.navigate(['/paciente/agenda'], {
      queryParams: {
        instituicaoId: this.conversa?.instituicaoId,
        coletaId: this.conversa?.coletaId
      }
    });
  }
}
