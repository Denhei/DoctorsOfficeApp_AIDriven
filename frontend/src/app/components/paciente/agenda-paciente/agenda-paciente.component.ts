import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ApiService, Instituicao, Medico, SlotAgenda } from '../../../services/api.service';
import { AuthService } from '../../../services/auth.service';

@Component({
  selector: 'app-agenda-paciente',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="container">
      <div class="page-header flex justify-between items-center">
        <div>
          <h2>📅 Agendamento de Consultas</h2>
          <p class="text-muted">Selecione uma data e um horário disponível com o médico da sua preferência.</p>
        </div>
      </div>

      <div *ngIf="sucessoMensagem" class="alert alert-success">
        ✅ {{ sucessoMensagem }}
      </div>

      <div *ngIf="erroMensagem" class="alert alert-danger">
        ⚠️ {{ erroMensagem }}
      </div>

      <!-- Filtros -->
      <div class="card filters-card">
        <div class="grid grid-cols-3 gap-4">
          <div class="form-group">
            <label class="form-label">Instituição</label>
            <select class="form-control" [(ngModel)]="filtroInstituicaoId" (change)="onFiltroChange()">
              <option value="">Todas as instituições</option>
              <option *ngFor="let inst of instituicoes" [value]="inst.id">{{ inst.nome }}</option>
            </select>
          </div>

          <div class="form-group">
            <label class="form-label">Médico / Especialidade</label>
            <select class="form-control" [(ngModel)]="filtroMedicoId" (change)="onFiltroChange()">
              <option value="">Todos os médicos</option>
              <option *ngFor="let m of medicos" [value]="m.id">{{ m.nome }} ({{ m.especialidade }})</option>
            </select>
          </div>

          <div class="form-group">
            <label class="form-label">Data</label>
            <input type="date" class="form-control" [(ngModel)]="filtroData" (change)="onFiltroChange()" />
          </div>
        </div>
      </div>

      <!-- Grade de Horários -->
      <div class="slots-section">
        <h3>Horários Disponíveis (Intervalos de 30 minutos)</h3>

        <div *ngIf="loading" class="text-muted">Carregando horários...</div>

        <div *ngIf="!loading && slots.length === 0" class="card empty-card">
          <p>Nenhum horário encontrado para os filtros selecionados.</p>
        </div>

        <div class="slots-grid" *ngIf="!loading && slots.length > 0">
          <div 
            *ngFor="let slot of slots" 
            class="slot-card card" 
            [ngClass]="{'disponivel': slot.disponivel, 'ocupado': !slot.disponivel}">
            <div class="slot-time">
              🕒 {{ slot.inicioUtc | date:'dd/MM/yyyy HH:mm' }} - {{ slot.fimUtc | date:'HH:mm' }}
            </div>
            <div class="slot-info">
              <strong>{{ slot.medicoNome }}</strong>
              <small>{{ slot.especialidade }} • 🏢 {{ slot.instituicaoNome }}</small>
            </div>
            <div class="slot-action">
              <button 
                *ngIf="slot.disponivel" 
                class="btn btn-primary btn-sm"
                (click)="abrirModalConfirmacao(slot)">
                Reservar Horário
              </button>
              <span *ngIf="!slot.disponivel" class="badge badge-secondary">Ocupado</span>
            </div>
          </div>
        </div>
      </div>

      <!-- Modal de Confirmação de Agendamento -->
      <div class="modal-overlay" *ngIf="slotSelecionado">
        <div class="modal card">
          <h3>Confirmar Agendamento</h3>
          <p><strong>Médico:</strong> {{ slotSelecionado.medicoNome }} ({{ slotSelecionado.especialidade }})</p>
          <p><strong>Unidade:</strong> {{ slotSelecionado.instituicaoNome }}</p>
          <p><strong>Horário:</strong> {{ slotSelecionado.inicioUtc | date:'dd/MM/yyyy HH:mm' }}</p>

          <div class="form-group" style="margin-top: 1rem;">
            <label class="form-label">ID da Triagem/Coleta Validada:</label>
            <input type="text" class="form-control" [(ngModel)]="coletaIdInput" placeholder="UUID da Coleta Validada" />
            <small class="text-muted">O agendamento só é aceito com uma coleta validada pelo enfermeiro nesta unidade.</small>
          </div>

          <div class="modal-actions flex justify-between">
            <button class="btn btn-secondary" (click)="slotSelecionado = null">Cancelar</button>
            <button class="btn btn-primary" (click)="confirmarAgendamento()" [disabled]="agendando">
              {{ agendando ? 'Agendando...' : 'Confirmar Reserva' }}
            </button>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .page-header { margin-bottom: 1.5rem; }
    .filters-card { margin-bottom: 1.5rem; }
    .slots-section h3 { margin-bottom: 1rem; font-size: 1.125rem; }
    .slots-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
      gap: 1rem;
    }
    .slot-card {
      display: flex;
      flex-direction: column;
      justify-content: space-between;
      gap: 0.75rem;
    }
    .slot-card.ocupado {
      opacity: 0.6;
      background: #f8fafc;
    }
    .slot-time { font-weight: 700; font-size: 0.9375rem; color: var(--secondary); }
    .slot-info { display: flex; flex-direction: column; }
    .slot-info small { color: var(--text-muted); }
    .empty-card { text-align: center; color: var(--text-muted); padding: 2rem; }
    .modal-overlay {
      position: fixed;
      inset: 0;
      background: rgba(0,0,0,0.5);
      display: flex;
      align-items: center;
      justify-content: center;
      z-index: 100;
    }
    .modal {
      width: 100%;
      max-width: 480px;
      padding: 1.5rem;
    }
    .modal-actions { margin-top: 1.5rem; }
  `]
})
export class AgendaPacienteComponent implements OnInit {
  private api = inject(ApiService);
  private auth = inject(AuthService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  instituicoes: Instituicao[] = [];
  medicos: Medico[] = [];
  slots: SlotAgenda[] = [];

  filtroInstituicaoId = '';
  filtroMedicoId = '';
  filtroData = '';

  slotSelecionado: SlotAgenda | null = null;
  coletaIdInput = '';
  loading = false;
  agendando = false;
  sucessoMensagem = '';
  erroMensagem = '';

  ngOnInit() {
    this.api.getInstituicoes().subscribe(res => this.instituicoes = res);
    this.api.getMedicos().subscribe(res => this.medicos = res);

    this.route.queryParams.subscribe(params => {
      if (params['instituicaoId']) this.filtroInstituicaoId = params['instituicaoId'];
      if (params['coletaId']) this.coletaIdInput = params['coletaId'];
      this.carregarSlots();
    });
  }

  onFiltroChange() {
    this.carregarSlots();
  }

  carregarSlots() {
    this.loading = true;
    this.api.getSlots(
      this.filtroMedicoId || undefined,
      this.filtroInstituicaoId || undefined,
      this.filtroData || undefined
    ).subscribe({
      next: (res) => {
        this.slots = res;
        this.loading = false;
      },
      error: () => this.loading = false
    });
  }

  abrirModalConfirmacao(slot: SlotAgenda) {
    this.slotSelecionado = slot;
    this.erroMensagem = '';
    this.sucessoMensagem = '';
  }

  confirmarAgendamento() {
    const user = this.auth.currentUser();
    if (!user?.pacienteId || !this.slotSelecionado) return;

    if (!this.coletaIdInput) {
      this.erroMensagem = 'Informe o ID da coleta validada pela enfermagem.';
      return;
    }

    this.agendando = true;
    this.erroMensagem = '';
    this.sucessoMensagem = '';

    this.api.agendarConsulta({
      pacienteId: user.pacienteId,
      slotId: this.slotSelecionado.id,
      coletaId: this.coletaIdInput
    }).subscribe({
      next: () => {
        this.agendando = false;
        this.slotSelecionado = null;
        this.sucessoMensagem = 'Consulta agendada com sucesso!';
        this.carregarSlots();
      },
      error: (err) => {
        this.agendando = false;
        this.erroMensagem = err.error?.mensagem || 'Erro ao agendar consulta. Verifique as regras de agendamento.';
      }
    });
  }
}
