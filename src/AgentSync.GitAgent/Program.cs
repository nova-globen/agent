using AgentSync.Cli;

// git-agent is the Git extension entry point: running `git agent <args>` invokes the
// `git-agent` binary with <args>. It simply delegates to the same CLI as `agent`.
return new CliRunner().Run(args);
