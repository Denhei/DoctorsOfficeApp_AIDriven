import { Routes } from '@angular/router';
import { LoginComponent } from './components/login/login.component';
import { ChatTriagemComponent } from './components/paciente/chat-triagem/chat-triagem.component';
import { AgendaPacienteComponent } from './components/paciente/agenda-paciente/agenda-paciente.component';
import { ConsultasPacienteComponent } from './components/paciente/consultas-paciente/consultas-paciente.component';
import { HistoricoPacienteComponent } from './components/paciente/historico-paciente/historico-paciente.component';
import { AutorizacoesPacienteComponent } from './components/paciente/autorizacoes-paciente/autorizacoes-paciente.component';
import { NotificacoesPacienteComponent } from './components/paciente/notificacoes-paciente/notificacoes-paciente.component';
import { PesquisaSatisfacaoComponent } from './components/paciente/pesquisa-satisfacao/pesquisa-satisfacao.component';
import { FilaEnfermeiroComponent } from './components/enfermeiro/fila-enfermeiro/fila-enfermeiro.component';
import { ConsultasMedicoComponent } from './components/medico/consultas-medico/consultas-medico.component';
import { DashboardAtendenteComponent } from './components/atendente/dashboard-atendente/dashboard-atendente.component';
import { AdminUsuariosComponent } from './components/admin/admin-usuarios/admin-usuarios.component';
import { AdminSaudeComponent } from './components/admin/admin-saude/admin-saude.component';

export const routes: Routes = [
  { path: '', redirectTo: 'login', pathMatch: 'full' },
  { path: 'login', component: LoginComponent },

  // Rotas Paciente
  { path: 'paciente/triagem', component: ChatTriagemComponent },
  { path: 'paciente/agenda', component: AgendaPacienteComponent },
  { path: 'paciente/consultas', component: ConsultasPacienteComponent },
  { path: 'paciente/historico', component: HistoricoPacienteComponent },
  { path: 'paciente/autorizacoes', component: AutorizacoesPacienteComponent },
  { path: 'paciente/notificacoes', component: NotificacoesPacienteComponent },
  { path: 'paciente/pesquisa/:id', component: PesquisaSatisfacaoComponent },

  // Rotas Enfermeiro
  { path: 'enfermeiro/fila', component: FilaEnfermeiroComponent },

  // Rotas Médico
  { path: 'medico/consultas', component: ConsultasMedicoComponent },
  { path: 'medico/agenda', component: AgendaPacienteComponent },

  // Rotas Atendente
  { path: 'atendente/dashboard', component: DashboardAtendenteComponent },
  { path: 'atendente/agenda', component: AgendaPacienteComponent },
  { path: 'atendente/pacientes', component: AdminUsuariosComponent },

  // Rotas Admin
  { path: 'admin/usuarios', component: AdminUsuariosComponent },
  { path: 'admin/saude', component: AdminSaudeComponent },

  { path: '**', redirectTo: 'login' }
];
