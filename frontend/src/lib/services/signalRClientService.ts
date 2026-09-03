import { signalRService } from './signalRService';
import { handleTaskCreated, handleTaskUpdated, handleTaskMoved, 
         handleTaskDeleted, handleTasksRebalanced, handleTaskAssigneeAdded,
         handleTaskAssigneeRemoved, handleTaskLabelAdded, handleTaskLabelRemoved,
         handleCommitLinked, handlePrLinked, handleAttachmentUploaded, 
         handleAttachmentDeleted } from '../stores/taskStore';
import { handleSprintCreated, handleSprintUpdated, handleSprintDeleted } from '../stores/sprintStore';
import { handleBoardCreated, handleBoardUpdated, handleBoardDeleted,
         handleColumnCreated, handleColumnUpdated, handleColumnDeleted,
         handleColumnsReordered } from '../stores/boardStore';
import { handleIntegrationCreated, handleIntegrationUpdated, 
         handleIntegrationVerified, handleIntegrationDeleted } from '../stores/integrationStore';
import { handleMemberAdded, handleMemberRemoved, handleMemberRoleUpdated } from '../stores/teamStore';
import { handleProjectUpdated, handleProjectArchived, 
         handleProjectUnarchived, handleProjectDeleted,
         handleLabelCreated, handleLabelDeleted, setProjects, setActiveProject } from '../stores/projectStore';
import { get } from 'svelte/store';
import { authStore } from '../stores/authStore';
import { push } from 'svelte-spa-router';
import { getProjectsAsync } from '../api/projectApi';
import { handleActivityCreated } from '../stores/activityStore';

export function registerSignalREvents() {
    // Task events
    signalRService.on('TaskCreated', handleTaskCreated);
    signalRService.on('TaskUpdated', handleTaskUpdated);
    signalRService.on('TaskMoved', handleTaskMoved);
    signalRService.on('TaskDeleted', handleTaskDeleted);
    signalRService.on('TasksRebalanced', handleTasksRebalanced);
    signalRService.on('TaskAssigneeAdded', handleTaskAssigneeAdded);
    signalRService.on('TaskAssigneeRemoved', handleTaskAssigneeRemoved);
    signalRService.on('TaskLabelAdded', handleTaskLabelAdded);
    signalRService.on('TaskLabelRemoved', handleTaskLabelRemoved);
    signalRService.on('CommitLinked', handleCommitLinked);
    signalRService.on('PrLinked', handlePrLinked);

    // Sprint events
    signalRService.on('SprintCreated', handleSprintCreated);
    signalRService.on('SprintUpdated', handleSprintUpdated);
    signalRService.on('SprintDeleted', handleSprintDeleted);

    // Board + Column events
    signalRService.on('BoardCreated', handleBoardCreated);
    signalRService.on('BoardUpdated', handleBoardUpdated);
    signalRService.on('BoardDeleted', handleBoardDeleted);
    signalRService.on('ColumnCreated', handleColumnCreated);
    signalRService.on('ColumnUpdated', handleColumnUpdated);
    signalRService.on('ColumnDeleted', handleColumnDeleted);
    signalRService.on('ColumnsReordered', handleColumnsReordered);

    // Integration events
    signalRService.on('IntegrationCreated', handleIntegrationCreated);
    signalRService.on('IntegrationUpdated', handleIntegrationUpdated);
    signalRService.on('IntegrationVerified', handleIntegrationVerified);
    signalRService.on('IntegrationDeleted', handleIntegrationDeleted);

    // Team events
    signalRService.on('MemberAdded', handleMemberAdded);
    signalRService.on('MemberRemoved', async (data) => {
        const currentUserId = get(authStore).user?.userId;
        if (data.userId === currentUserId) {
            const projects = await getProjectsAsync();
            setProjects(projects);
            setActiveProject(null);
            await new Promise(resolve => setTimeout(resolve, 100));
            push('/app');
        } else {
            handleMemberRemoved(data);
        }
    });
    signalRService.on('MemberRoleUpdated', handleMemberRoleUpdated);

    // Project events
    signalRService.on('ProjectUpdated', handleProjectUpdated);
    signalRService.on('ProjectArchived', handleProjectArchived);
    signalRService.on('ProjectUnarchived', handleProjectUnarchived);
    signalRService.on('ProjectDeleted', handleProjectDeleted);

    // Label events
    signalRService.on('LabelCreated', handleLabelCreated);
    signalRService.on('LabelDeleted', handleLabelDeleted);

    // Activity events
    signalRService.on('ActivityCreated', handleActivityCreated);

    // Attachment
    signalRService.on('AttachmentUploaded', handleAttachmentUploaded);
    signalRService.on('AttachmentDeleted', handleAttachmentDeleted);
}

export function unregisterSignalREvents() {
    signalRService.off('TaskCreated');
    signalRService.off('TaskUpdated');
    signalRService.off('TaskMoved');
    signalRService.off('TaskDeleted');
    signalRService.off('TasksRebalanced');
    signalRService.off('TaskAssigneeAdded');
    signalRService.off('TaskAssigneeRemoved');
    signalRService.off('TaskLabelAdded');
    signalRService.off('TaskLabelRemoved');
    signalRService.off('CommitLinked');
    signalRService.off('PrLinked');
    signalRService.off('SprintCreated');
    signalRService.off('SprintUpdated');
    signalRService.off('SprintDeleted');
    signalRService.off('BoardCreated');
    signalRService.off('BoardUpdated');
    signalRService.off('BoardDeleted');
    signalRService.off('ColumnCreated');
    signalRService.off('ColumnUpdated');
    signalRService.off('ColumnDeleted');
    signalRService.off('ColumnsReordered');
    signalRService.off('IntegrationCreated');
    signalRService.off('IntegrationUpdated');
    signalRService.off('IntegrationVerified');
    signalRService.off('IntegrationDeleted');
    signalRService.off('MemberAdded');
    signalRService.off('MemberRemoved');
    signalRService.off('MemberRoleUpdated');
    signalRService.off('ProjectUpdated');
    signalRService.off('ProjectArchived');
    signalRService.off('ProjectUnarchived');
    signalRService.off('ProjectDeleted');
    signalRService.off('LabelCreated');
    signalRService.off('LabelDeleted');
    signalRService.off('ActivityCreated');
}