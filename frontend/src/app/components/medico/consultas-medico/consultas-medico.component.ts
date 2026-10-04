import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService, Consulta, HistoricoPaciente } from '../../../services/api.service';
import { AuthService } from '../../../services/auth.service';

@Component({
  selector: 'app-consultas-medico',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="container">
      <div class="page-header flex justify-between items-center">
        <div>
          <h2>👨‍⚕️ Atendimentos Médicos & Prontuário</h2>
          <p class="text-muted">Consulte sua agenda, acesse dados clínicos autorizados e registre evoluções médicas.</p>
        </div>
        <button class="btn btn-secondary btn-sm" (click)="carregarConsultas()">🔄 Atualizar</button>
      </div>

      <div *ngIf="loading" class="text-muted">Carregando atendimentos...</div>

      <div *ngIf="!loading && consultas.length === 0" class="card empty-card">
        <p>Nenhum atendimento encontrado em sua escala.</p>
      </div>

      <div class="consultas-grid" *ngIf="!loading && consultas.length > 0">
        <div *ngFor="let c of consultas" class="card consulta-card">
          <div class="flex justify-between items-center">
            <span class="badge" [ngClass]="getStatusBadge(c.status)">{{ c.status }}</span>
            <small class="text-muted">{{ c.inicioUtc | date:'dd/MM/yyyy HH:mm' }}</small>
          </div>

          <div class="card-body">
            <h3>👤 {{ c.pacienteNome }}</h3>
            <p class="text-muted">🏢 {{ c.instituicaoNome }} • {{ c.especialidade }}</p>

            <div class="resumo-box" *ngIf="c.resumoValidado">
              <strong>🩺 Resumo Validado pelo Enfermeiro:</strong>
              <p>{{ c.resumoValidado }}</p>
            </div>
          </div>

          <div class="card-actions flex justify-between items-center">
            <button class="btn btn-secondary btn-sm" (click)="abrirProntuario(c)">
              📁 Prontuário & Histórico
            </button>

            <button 
              *ngIf="c.status === 'Agendada' || c.status === 'EmAtendimento'" 
              class="btn btn-primary btn-sm"
              (click)="iniciarAtendimento(c)">
              🩺 {{ c.status === 'Agendada' ? 'Iniciar Consulta' : 'Continuar Atendimento' }}
            </button>

            <span *ngIf="c.status === 'Concluida'" class="badge badge-success">
              ✓ Atendimento Concluído
            </span>
          </div>
        </div>
      </div>

      <!-- Modal de Atendimento & Evolução Médica -->
      <div class="modal-overlay" *ngIf="consultaEmAtendimento">
        <div class="modal card">
          <div class="flex justify-between items-center">
            <h3>Atendimento: {{ consultaEmAtendimento.pacienteNome }}</h3>
            <button class="btn-close" (click)="consultaEmAtendimento = null">✕</button>
          </div>

          <div class="modal-body">
            <div class="alert alert-info">
              <strong>Resumo da Triagem Validada:</strong><br />
              {{ consultaEmAtendimento.resumoValidado }}
            </div>

            <div class="form-group">
              <label class="form-label">Evolução Clínica / Conduta Médica (Obrigatório):</label>
              <textarea 
                class="form-control" 
                rows="6" 
                [(ngModel)]="evolucaoTextoInput" 
                placeholder="Descreva a história clínica, exame físico, hipóteses diagnósticas e prescrição/conduta..."></textarea>
            </div>

            <div class="actions flex justify-between items-center">
              <button class="btn btn-secondary" (click)="salvarRascunhoEvolucao()" [disabled]="salvando">
                💾 Salvar Rascunho
              </button>

              <button class="btn btn-primary" (click)="concluirAtendimento()" [disabled]="salvando || !evolucaoTextoInput.trim()">
                🏁 Concluir Consulta & Disparar Notificações
              </button>
            </div>
          </div>
        </div>
      </div>

      <!-- Modal de Prontuário Longitudinal & Anexos -->
      <div class="modal-overlay" *ngIf="prontuarioPaciente">
        <div class="modal card">
          <div class="flex justify-between items-center">
            <h3>Prontuário de {{ prontuarioPaciente.nome }}</h3>
            <button class="btn-close" (click)="prontuarioPaciente = null">✕</button>
          </div>

          <div class="modal-body">
            <h4>Evoluções Anteriores Autorizadas</h4>
            <div *ngIf="prontuarioPaciente.evolucoes.length === 0" class="text-muted">
              Nenhuma evolução clínica prévia disponível ou autorizada para esta instituição.
            </div>
            <div *ngFor="let ev of prontuarioPaciente.evolucoes" class="card" style="background:#f8fafc; margin-bottom: 0.5rem;">
              <strong>👨‍⚕️ {{ ev.medicoNome }}</strong> ({{ ev.criadaEmUtc | date:'dd/MM/yyyy' }}):
              <p>{{ ev.texto }}</p>
            </div>

            <h4 style="margin-top: 1rem;">Documentos e Exames Anexados</h4>
            <div *ngIf="prontuarioPaciente.documentos.length === 0" class="text-muted">
              Nenhum documento disponível ou autorizado.
            </div>
            <div *ngFor="let d of prontuarioPaciente.documentos" class="flex justify-between items-center card" style="background:#f8fafc; margin-bottom: 0.5rem; padding: 0.75rem;">
              <div>
                <strong>📄 {{ d.nomeOriginal }}</strong><br />
                <small class="text-muted">Origem: {{ d.instituicaoOrigemNome }}</small>
              </div>
              <button class="btn btn-secondary btn-sm" (click)="baixarDoc(d.id, d.nomeOriginal)">⬇ Baixar</button>
            </div>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .page-header { margin-bottom: 1.5rem; }
    .consultas-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(320px, 1fr)); gap: 1rem; }
    .consulta-card { display: flex; flex-direction: column; justify-content: space-between; gap: 1rem; }
    .resumo-box { background: #f8fafc; padding: 0.625rem; border-radius: 6px; font-size: 0.8125rem; margin-top: 0.75rem; }
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
    .modal { width: 100%; max-width: 680px; max-height: 90vh; overflow-y: auto; padding: 1.5rem; }
    .modal-body { margin-top: 1rem; display: flex; flex-direction: column; gap: 1rem; }
    .btn-close { background: none; border: none; font-size: 1.25rem; cursor: pointer; }
  `]
})
export class ConsultasMedicoComponent implements OnInit {
  private api = inject(ApiService);
  private auth = inject(AuthService);

  consultas: Consulta[] = [];
  consultaEmAtendimento: Consulta | null = null;
  prontuarioPaciente: HistoricoPaciente | null = null;
  evolucaoTextoInput = '';
  loading = false;
  salvando = false;

  ngOnInit() {
    this.carregarConsultas();
  }

  carregarConsultas() {
    this.loading = true;
    this.api.getConsultas().subscribe({
      next: (res) => {
        this.consultas = res;
        this.loading = false;
      },
      error: () => this.loading = false
    });
  }

  iniciarAtendimento(c: Consulta) {
    this.consultaEmAtendimento = c;
    this.evolucaoTextoInput = '';
    if (c.status === 'Agendada') {
      this.api.atualizarStatusConsulta(c.id, 'EmAtendimento').subscribe();
    }
  }

  salvarRascunhoEvolucao() {
    if (!this.consultaEmAtendimento || !this.evolucaoTextoInput.trim()) return;
    this.salvando = true;
    this.api.registrarEvolucao(this.consultaEmAtendimento.id, this.evolucaoTextoInput).subscribe({
      next: () => {
        this.salvando = false;
        alert('Evolução salva com sucesso!');
      },
      error: (err) => {
        this.salvando = false;
        alert(err.error?.mensagem || 'Erro ao salvar evolução.');
      }
    });
  }

  concluirAtendimento() {
    if (!this.consultaEmAtendimento || !this.evolucaoTextoInput.trim()) return;
    this.salvando = true;
    this.api.registrarEvolucao(this.consultaEmAtendimento.id, this.evolucaoTextoInput).subscribe({
      next: () => {
        this.api.concluirConsulta(this.consultaEmAtendimento!.id).subscribe({
          next: () => {
            this.salvando = false;
            this.consultaEmAtendimento = null;
            this.carregarConsultas();
          },
          error: (err) => {
            this.salvando = false;
            alert(err.error?.mensagem || 'Erro ao concluir consulta.');
          }
        });
      },
      error: (err) => {
        this.salvando = false;
        alert(err.error?.mensagem || 'Erro ao salvar evolução.');
      }
    });
  }

  abrirProntuario(c: Consulta) {
    this.api.getHistoricoPaciente(c.pacienteId).subscribe({
      next: (res) => this.prontuarioPaciente = res,
      error: (err) => alert(err.error?.mensagem || 'Acesso ao prontuário negado.')
    });
  }

  baixarDoc(docId: string, nome: string) {
    this.api.baixarDocumento(docId).subscribe(blob => {
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = nome;
      a.click();
      window.URL.revokeObjectURL(url);
    });
  }

  getStatusBadge(status: string): string {
    switch (status) {
      case 'Agendada': return 'badge-primary';
      case 'EmAtendimento': return 'badge-warning';
      case 'Concluida': return 'badge-success';
      case 'Cancelada': return 'badge-danger';
      default: return 'badge-secondary';
    }
  }
}
