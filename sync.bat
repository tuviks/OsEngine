@echo off
echo = Getting updates from a public repository (upstream) =
git fetch upstream

echo = Updating local branch main from upstream/main =
git checkout master
git merge upstream/master

echo = Publishing updates to fork and private =
git push fork master
git push private master

echo = Synchronization completed =
pause
