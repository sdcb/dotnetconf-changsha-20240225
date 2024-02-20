<Query Kind="Statements">
  <NuGetReference Prerelease="true">Sdcb.DashScope</NuGetReference>
  <Namespace>Sdcb.DashScope</Namespace>
  <Namespace>System.Threading.Tasks</Namespace>
  <Namespace>Sdcb.DashScope.WanXiang</Namespace>
</Query>

using DashScopeClient api = new(Util.GetPassword("dashscope-api-key"));
string url = "https://io.starworks.cc:88/cv-public/2023/1317141.jpg";
DashScopeTask task = await api.WanXiang.StyleReplicate(new StyleReplicationInput { ImageUrl = url });
var dc = new DumpContainer().Dump();
while (true)
{
	TaskStatusResponse status = await api.QueryTaskStatus(task.TaskId);
	dc.Content = status;
	if (status.TaskStatus == DashScopeTaskStatus.Succeeded)
	{
		dc.Content = status.AsSuccess();
		Util.HorizontalRun(false, status.AsSuccess().Results
			.Where(x => x.IsSuccess)
			.Select(x => Util.Image(x.Url!, Util.ScaleMode.Unscaled))).Dump();
		break;
	}
	else if (status.TaskStatus == DashScopeTaskStatus.Failed)
	{
		dc.Content = status.AsFailed();
		break;
	}
	await Task.Delay(1000);
}