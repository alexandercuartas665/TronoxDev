using Tronox.Application.Workflows;
using Tronox.Domain.Enums;

namespace Tronox.Application.Tests;

/// <summary>
/// Tests PUROS del motor de flujos BPMN (RQ11, port de ECOREX). Sin EF: parser/writer de XML
/// (round-trip), evaluador de condiciones de compuertas y resolver de candidatos por organigrama.
/// </summary>
public class WorkflowBpmnTests
{
    private const string ValidBpmn = """
        <?xml version="1.0" encoding="UTF-8"?>
        <bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL" id="D">
          <bpmn:process id="P" isExecutable="true">
            <bpmn:startEvent id="Start_1" name="Inicio" />
            <bpmn:task id="Task_1" name="Revisar" />
            <bpmn:exclusiveGateway id="Gw_1" name="Aprobado?" />
            <bpmn:endEvent id="End_1" name="Fin" />
            <bpmn:sequenceFlow id="F1" sourceRef="Start_1" targetRef="Task_1" />
            <bpmn:sequenceFlow id="F2" sourceRef="Task_1" targetRef="Gw_1" />
            <bpmn:sequenceFlow id="F3" sourceRef="Gw_1" targetRef="End_1">
              <bpmn:conditionExpression>approval == 'Approved'</bpmn:conditionExpression>
            </bpmn:sequenceFlow>
          </bpmn:process>
        </bpmn:definitions>
        """;

    [Fact]
    public void Parse_FlujoValido_SinErrores()
    {
        var parsed = BpmnProcessParser.Parse(ValidBpmn);
        Assert.True(parsed.IsValid);
        Assert.Equal(4, parsed.Nodes.Count);
        Assert.Equal(3, parsed.Edges.Count);
        Assert.Single(parsed.Nodes, n => n.NodeType == WorkflowNodeType.StartEvent);
        Assert.Equal("approval == 'Approved'", parsed.Edges.Single(e => e.BpmnElementId == "F3").ConditionExpression);
    }

    [Fact]
    public void Parse_SinStartEvent_Invalido()
    {
        var xml = ValidBpmn.Replace("<bpmn:startEvent id=\"Start_1\" name=\"Inicio\" />", "");
        var parsed = BpmnProcessParser.Parse(xml);
        Assert.False(parsed.IsValid);
        Assert.Contains(parsed.Errors, e => e.Contains("startEvent"));
    }

    [Fact]
    public void Parse_SinEndEvent_Invalido()
    {
        var xml = ValidBpmn.Replace("<bpmn:endEvent id=\"End_1\" name=\"Fin\" />", "");
        var parsed = BpmnProcessParser.Parse(xml);
        Assert.False(parsed.IsValid);
        Assert.Contains(parsed.Errors, e => e.Contains("endEvent"));
    }

    [Fact]
    public void WriteThenParse_ReproduceElGrafo()
    {
        var nodes = new List<BpmnWriterNode>
        {
            new("Start_1", "Inicio", WorkflowNodeType.StartEvent, 60, 90, 46, 46),
            new("Task_1", "Revisar", WorkflowNodeType.Task, 200, 80, 140, 64),
            new("End_1", "Fin", WorkflowNodeType.EndEvent, 400, 90, 46, 46),
        };
        var edges = new List<BpmnWriterEdge>
        {
            new("F1", "Start_1", "Task_1", null, null),
            new("F2", "Task_1", "End_1", null, null),
        };

        var xml = BpmnXmlWriter.Write("COT-COM", nodes, edges);
        var parsed = BpmnProcessParser.Parse(xml);

        Assert.True(parsed.IsValid);
        Assert.Equal(3, parsed.Nodes.Count);
        Assert.Equal(2, parsed.Edges.Count);
        // Las coordenadas del canvas sobreviven el round-trip (DI del bpmndi).
        var task = parsed.Nodes.Single(n => n.BpmnElementId == "Task_1");
        Assert.Equal(200, task.X);
        Assert.Equal(140, task.W);
    }

    [Theory]
    [InlineData("approval == 'Approved'", "Approved", true)]
    [InlineData("approval == 'Approved'", "Rejected", false)]
    [InlineData("approval != 'Rejected'", "Approved", true)]
    [InlineData("approval != 'Rejected'", "Rejected", false)]
    [InlineData("approval == \"Approved\"", "approved", true)]  // case-insensitive, comillas dobles
    [InlineData("", "Approved", false)]                          // default: no aplica por condicion
    [InlineData("otra_cosa > 5", "Approved", false)]             // formato desconocido: fail-closed
    public void ConditionEvaluator(string expr, string approval, bool expected)
    {
        Assert.Equal(expected, WorkflowConditionEvaluator.Evaluate(expr, approval));
    }

    [Fact]
    public void ConditionEvaluator_IsDefault()
    {
        Assert.True(WorkflowConditionEvaluator.IsDefault(null));
        Assert.True(WorkflowConditionEvaluator.IsDefault("  "));
        Assert.False(WorkflowConditionEvaluator.IsDefault("approval == 'X'"));
    }

    [Fact]
    public void OrgAssigneeTree_ExpandeDependenciaAFuncionariosMiembrosYResponsable()
    {
        // Dependencia(1) -> Cargo(2) -> Funcionario(3, ocupa user 100)
        //                -> responsable de la dependencia = user 200
        // Cargo(2) tiene miembro user 300.
        var units = new List<OrgAssigneeTree.UnitRow>
        {
            new(1, null, OrgUnitClassifier.Dependencia, ResponsibleTenantUserId: 200, TenantUserId: null),
            new(2, 1, OrgUnitClassifier.Cargo, null, null),
            new(3, 2, OrgUnitClassifier.Funcionario, null, TenantUserId: 100),
        };
        var members = new List<OrgAssigneeTree.MemberRow> { new(2, 300) };

        var candidates = OrgAssigneeTree.ResolveForUnit(1, units, members);

        Assert.Equal(3, candidates.Count);
        Assert.Contains(100L, candidates); // funcionario ocupante
        Assert.Contains(200L, candidates); // responsable de la dependencia
        Assert.Contains(300L, candidates); // miembro del cargo
    }

    [Fact]
    public void OrgAssigneeTree_SoloElCargo_NoIncluyeHermanos()
    {
        var units = new List<OrgAssigneeTree.UnitRow>
        {
            new(1, null, OrgUnitClassifier.Dependencia, null, null),
            new(2, 1, OrgUnitClassifier.Cargo, null, null),
            new(3, 2, OrgUnitClassifier.Funcionario, null, TenantUserId: 100),
            new(4, 1, OrgUnitClassifier.Cargo, null, null),
            new(5, 4, OrgUnitClassifier.Funcionario, null, TenantUserId: 999),
        };

        var candidates = OrgAssigneeTree.ResolveForUnit(2, units, []);

        Assert.Single(candidates);
        Assert.Contains(100L, candidates);
        Assert.DoesNotContain(999L, candidates); // otro cargo hermano no entra
    }
}
