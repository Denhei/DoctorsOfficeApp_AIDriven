import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService, ColetaItemFila, ConversaDetalhe } from '../../../services/api.service';

@Component({
  selector: 'app-fila-enfermeiro',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="container">
      <div class="page-header flex justify-between items-center">
        <div>
          <h2>🩺 Fila de Triagem da Enfermagem</h2>
          <p class="text-muted">Revise as coletas preliminares guiadas, ajuste as informações e valide para liberar o agendamento.</p>
        </div>
        <button class="btn btn-secondary btn-sm" (click)="carregarFila()">🔄 Atualizar Fila</button>
      </div>

      <div *ngIf="loading" class="text-muted">Carregando fila...</div>

      <div *ngIf="!loading && fila.length === 0" class="card empty-card">
        <p>✅ Nenhuma coleta pendente de validação para sua instituição no momento.</p>
      </div>

      <div class="fila-grid" *ngIf="!loading && fila.length > 0">
        <div *ngFor="let item of fila" class="card item-card flex justify-between items-center">
          <div>
            <div class="flex items-center gap-2">
              <span class="badge badge-warning">Pendente Validação</span>
              <strong>👤 {{ item.pacienteNome }}</strong>
            </div>
            <p class="resumo-previa">{{ item.resumoPreliminarTexto || 'Sem resumo preliminar' }}</p>
            <small class="text-muted">Enviado em: {{ item.criadaEmUtc | date:'dd/MM/yyyy HH:mm' }} • 🏢 {{ item.instituicaoNome }}</small>
          </div>

          <button class="btn btn-primary" (click)="abrirModalValidacao(item.id)">
            Revisar & Validar
          </button>
        </div>
      </div>

      <!-- Modal de Detalhe e Validação da Coleta -->
      <div class="modal-overlay" *ngIf="coletaDetalhe">
        <div class="modal card">
          <div class="flex justify-between items-center">
            <h3>Triagem: {{ coletaDetalhe.pacienteNome }}</h3>
            <button class="btn-close" (click)="fecharModal()">✕</button>
          </div>

          <div class="modal-body">
            <!-- Transcrição do Chat no MongoDB -->
            <div class="chat-transcript-box">
              <h4>💬 Transcrição Completa da Conversa</h4>
              <div class="transcript-messages">
                <div *ngFor="let m of coletaDetalhe.mensagens" class="transcript-msg" [ngClass]="m.remetente === 'Paciente' ? 'pac' : 'bot'">
                  <strong>{{ m.remetente }}:</strong> {{ m.conteudo }}
                  <small>{{ m.enviadaEmUtc | date:'HH:mm' }}</small>
                </div>
              </div>
            </div>

            <!-- Formulário de Correção e Validação -->
            <div class="validation-form">
              <h4>📋 Resumo a ser Validado (Edição Humana)</h4>
              <div class="form-group">
                <label class="form-label">Texto do Resumo Clínico Oficial:</label>
                <textarea class="form-control" rows="4" [(ngModel)]="resumoCorrigidoInput"></textarea>
              </div>

              <div class="form-group">
                <label class="form-label">Observações da Enfermagem (Opcional):</label>
                <input type="text" class="form-control" [(ngModel)]="observacaoInput" placeholder="Orientações adicionais..." />
              </div>

              <div class="actions flex justify-between">
                <button class="btn btn-danger" (click)="validar(false)" [disabled]="salvando">
                  ⚠️ Encaminhar p/ Presencial
                </button>
                <button class="btn btn-primary" (click)="validar(true)" [disabled]="salvando || !resumoCorrigidoInput.trim()">
                  ✅ Validar e Liberar Agendamento
                </button>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .page-header { margin-bottom: 1.5rem; }
    .fila-grid { display: flex; flex-direction: column; gap: 1rem; }
    .resumo-previa { font-size: 0.875rem; color: #334155; margin: 0.5rem 0 0.25rem 0; }
    .empty-card { text-align: center; color: var(--text-muted); padding: 2rem; }
    .modal-overlay {
      position: fixed;
      inset: 0;
      background: rgba(0,0,0,0.6);
      display: flex;
      align-items: center;
      justify-content: center;
      z-index: 100;
    }
    .modal {
      width: 100%;
      max-width: 750px;
      max-height: 90vh;
      display: flex;
      flex-direction: column;
      padding: 1.5rem;
    }
    .modal-body {
      overflow-y: auto;
      display: flex;
      flex-direction: column;
      gap: 1.25rem;
      margin-top: 1rem;
    }
    .chat-transcript-box {
      background: #f8fafc;
      border: 1px solid var(--border);
      border-radius: 8px;
      padding: 1rem;
      max-height: 250px;
      overflow-y: auto;
    }
    .transcript-messages { display: flex; flex-direction: column; gap: 0.5rem; margin-top: 0.5rem; }
    .transcript-msg { font-size: 0.8125rem; padding: 0.375rem 0.5rem; border-radius: 4px; }
    .transcript-msg.pac { background: #dcfce7; }
    .transcript-msg.bot { background: white; border: 1px solid #e2e8f0; }
    .transcript-msg small { color: #64748b; margin-left: 0.5rem; }
    .btn-close { background: none; border: none; font-size: 1.25rem; cursor: pointer; }
  `]
})
export class FilaEnfermeiroComponent implements OnInit {
  private api = inject(ApiService);

  fila: ColetaItemFila[] = [];
  coletaDetalhe: ConversaDetalhe | null = null;
  resumoCorrigidoInput = '';
  observacaoInput = '';
  loading = false;
  salvando = false;

  ngOnInit() {
    this.carregarFila();
  }

  carregarFila() {
    this.loading = true;
    this.api.getFilaEnfermeiro().subscribe({
      next: (res) => {
        this.fila = res;
        this.loading = false;
      },
      error: () => this.loading = false
    });
  }

  abrirModalValidacao(coletaId: string) {
    this.api.getColeta(coletaId).subscribe(res => {
      this.coletaDetalhe = res;
      this.resumoCorrigidoInput = `Queixa relatada: ${res.resumoPreliminar.queixa}. Duração relatada: ${res.resumoPreliminar.duracao}. Alergias relatadas: ${res.resumoPreliminar.alergias}. Medicamentos relatados: ${res.resumoPreliminar.medicamentos}. Observações: ${res.resumoPreliminar.observacoes}.`;
      this.observacaoInput = '';
    });
  }

  fecharModal() {
    this.coletaDetalhe = null;
  }

  validar(aprovado: boolean) {
    if (!this.coletaDetalhe) return;
    this.salvando = true;
    this.api.validarColeta(this.coletaDetalhe.coletaId, {
      aprovadoParaAgendamento: aprovado,
      resumoCorrigido: this.resumoCorrigidoInput,
      observacaoEnfermeiro: this.observacaoInput
    }).subscribe({
      next: () => {
        this.salvando = false;
        this.fecharModal();
        this.carregarFila();
      },
      error: (err) => {
        this.salvando = false;
        alert(err.error?.mensagem || 'Erro ao validar triagem.');
      }
    });
  }
}
