<Query Kind="Statements">
  <NuGetReference Prerelease="true">Sdcb.DashScope</NuGetReference>
  <Namespace>System.Diagnostics.CodeAnalysis</Namespace>
  <Namespace>System.Globalization</Namespace>
  <Namespace>System.Net.Http</Namespace>
  <Namespace>System.Net.Http.Headers</Namespace>
  <Namespace>System.Net.Http.Json</Namespace>
  <Namespace>System.Text.Json</Namespace>
  <Namespace>System.Text.Json.Serialization</Namespace>
  <Namespace>System.Threading.Tasks</Namespace>
  <Namespace>Sdcb.DashScope</Namespace>
  <Namespace>Sdcb.DashScope.StableDiffusion</Namespace>
  <RuntimeVersion>7.0</RuntimeVersion>
</Query>

using DashScopeClient api = new(Util.GetPassword("dashscope-api-key"));
Text2ImagePrompt prompt = new()
{
	Prompt = "standing, ultra detailed, official art, 4k 8k wallpaper, soft light and shadow, hand detail, eye high detail, 8K, (best quality:1.5), pastel color, soft focus, masterpiece, studio, hair high detail, (pure background:1.2), (head fully visible, full body shot)",
	NegativePrompt = "EasyNegative, nsfw,(low quality, worst quality:1.4),lamp, missing shoe, missing head,mutated hands and fingers,deformed,bad anatomy,extra limb,ugly,poorly drawn hands,disconnected limbs,missing limb,missing head,camera"
};
DashScopeTask task = await api.WanXiang.Text2Image(prompt, new Text2ImageParams { N = 4 });

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