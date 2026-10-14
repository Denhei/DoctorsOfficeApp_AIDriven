import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService, HistoricoPaciente, Instituicao } from '../../../services/api.service';
import { AuthService } from '../../../services/auth.service';

@Component({
  selector: 'app-historico-paciente',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="container">
      <div class="page-header">
        <h2>📁 Histórico Clínico & Documentos</h2>
        <p class="text-muted">Acesse suas evoluções médicas anteriores, exames e documentos anexados.</p>
      </div>

      <div *ngIf="loading" class="text-muted">Carregando histórico...</div>

      <div *ngIf="!loading && historico" class="content-layout">
        <!-- Evoluções Médicas -->
        <div class="section">
          <h3>Evoluções Médicas Registradas</h3>
          <div *ngIf="historico.evolucoes.length === 0" class="card empty-card">
            <p>Nenhuma evolução médica registrada até o momento.</p>
          </div>

          <div class="evolucoes-list" *ngIf="historico.evolucoes.length > 0">
            <div *ngFor="let ev of historico.evolucoes" class="card evolucao-card">
              <div class="flex justify-between items-center">
                <strong>👨‍⚕️ {{ ev.medicoNome }}</strong>
                <small class="text-muted">{{ ev.criadaEmUtc | date:'dd/MM/yyyy HH:mm' }}</small>
              </div>
              <p class="evolucao-text">{{ ev.texto }}</p>
            </div>
          </div>
        </div>

        <!-- Documentos e Exames Anexados -->
        <div class="section" style="margin-top: 2rem;">
          <div class="flex justify-between items-center" style="margin-bottom: 1rem;">
            <h3>Documentos e Anexos (PDF / Imagens - Máx 5MB)</h3>
            <button class="btn btn-primary btn-sm" (click)="mostrarFormUpload = !mostrarFormUpload">
              {{ mostrarFormUpload ? 'Fechar Formulário' : '+ Anexar Novo Documento' }}
            </button>
          </div>

          <!-- Formulário de Upload -->
          <div *ngIf="mostrarFormUpload" class="card upload-card">
            <h4>Anexar Arquivo</h4>
            <div class="form-group">
              <label class="form-label">Instituição de Origem do Exame:</label>
              <select class="form-control" [(ngModel)]="uploadInstituicaoId">
                <option *ngFor="let inst of instituicoes" [value]="inst.id">{{ inst.nome }}</option>
              </select>
            </div>

            <div class="form-group">
              <label class="form-label">Arquivo (PDF, JPG, PNG até 5MB):</label>
              <input type="file" (change)="onFileSelected($event)" class="form-control" accept=".pdf,.jpg,.jpeg,.png" />
            </div>

            <button class="btn btn-primary" (click)="fazerUpload()" [disabled]="!arquivoSelecionado || enviando">
              {{ enviando ? 'Enviando...' : 'Fazer Upload' }}
            </button>
          </div>

          <div *ngIf="historico.documentos.length === 0" class="card empty-card">
            <p>Nenhum documento anexado.</p>
          </div>

          <div class="docs-grid" *ngIf="historico.documentos.length > 0">
            <div *ngFor="let doc of historico.documentos" class="card doc-card flex justify-between items-center">
              <div class="doc-info">
                <span class="doc-icon">📄</span>
                <div>
                  <strong>{{ doc.nomeOriginal }}</strong><br />
                  <small class="text-muted">🏢 {{ doc.instituicaoOrigemNome }} • {{ (doc.tamanhoBytes / 1024) | number:'1.0-1' }} KB • {{ doc.criadoEmUtc | date:'dd/MM/yyyy' }}</small>
                </div>
              </div>
              <button class="btn btn-secondary btn-sm" (click)="baixar(doc.id, doc.nomeOriginal)">
                ⬇ Baixar
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .page-header { margin-bottom: 1.5rem; }
    .evolucoes-list { display: flex; flex-direction: column; gap: 1rem; }
    .evolucao-card { background: white; border-left: 4px solid var(--primary); }
    .evolucao-text { margin-top: 0.75rem; line-height: 1.6; }
    .upload-card { margin-bottom: 1.5rem; background: #f8fafc; }
    .docs-grid { display: flex; flex-direction: column; gap: 0.75rem; }
    .doc-info { display: flex; align-items: center; gap: 0.75rem; }
    .doc-icon { font-size: 1.5rem; }
    .empty-card { text-align: center; color: var(--text-muted); padding: 2rem; }
  `]
})
export class HistoricoPacienteComponent implements OnInit {
  private api = inject(ApiService);
  private auth = inject(AuthService);

  historico: HistoricoPaciente | null = null;
  instituicoes: Instituicao[] = [];
  loading = false;
  mostrarFormUpload = false;
  uploadInstituicaoId = '';
  arquivoSelecionado: File | null = null;
  enviando = false;

  ngOnInit() {
    this.carregarHistorico();
    this.api.getInstituicoes().subscribe(res => {
      this.instituicoes = res;
      if (res.length > 0) this.uploadInstituicaoId = res[0].id;
    });
  }

  carregarHistorico() {
    const user = this.auth.currentUser();
    if (!user?.pacienteId) return;

    this.loading = true;
    this.api.getHistoricoPaciente(user.pacienteId).subscribe({
      next: (res) => {
        this.historico = res;
        this.loading = false;
      },
      error: () => this.loading = false
    });
  }

  onFileSelected(event: Event) {
    const arquivos = (event.target as HTMLInputElement).files;
    if (arquivos?.length) {
      this.arquivoSelecionado = arquivos[0];
    }
  }

  fazerUpload() {
    const user = this.auth.currentUser();
    if (!user?.pacienteId || !this.arquivoSelecionado || !this.uploadInstituicaoId) return;

    this.enviando = true;
    this.api.anexarDocumento(user.pacienteId, this.uploadInstituicaoId, this.arquivoSelecionado).subscribe({
      next: () => {
        this.enviando = false;
        this.mostrarFormUpload = false;
        this.arquivoSelecionado = null;
        this.carregarHistorico();
      },
      error: (err) => {
        this.enviando = false;
        alert(err.error?.mensagem || 'Erro ao fazer upload.');
      }
    });
  }

  baixar(docId: string, nomeArquivo: string) {
    this.api.baixarDocumento(docId).subscribe(blob => {
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = nomeArquivo;
      a.click();
      window.URL.revokeObjectURL(url);
    });
  }
}
